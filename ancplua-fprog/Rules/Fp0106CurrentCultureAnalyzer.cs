using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0106 reports operations that implicitly read the current culture.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0106CurrentCultureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0106",
        "Pass the culture or comparer explicitly",
        "Supply a culture or ordinal comparer explicitly");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeConcatenation, OperationKind.Binary);
        context.RegisterSyntaxNodeAction(AnalyzeInterpolation, SyntaxKind.InterpolatedStringExpression);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (invocation.Syntax is not InvocationExpressionSyntax syntax
            || !CoreScope.IsInCore(context.ContainingSymbol)
            || Purity.Classify(method.ContainingType, context.Options.AnalyzerConfigOptionsProvider,
                syntax.SyntaxTree) != PurityClass.Pure
            || IsCultureFree(method.ContainingType)
            || method is { Name: "ToString", ContainingType.SpecialType: SpecialType.System_String })
            return;

        if (!IsCultureSensitiveStringComparison(method)
            && !SortsStrings(invocation)
            && !HasProviderOverload(method))
            return;

        var name = MemberSyntax.NameToken(syntax);
        if (name.RawKind != 0)
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation()));
    }

    private static bool HasProviderOverload(IMethodSymbol method)
    {
        method = method.ReducedFrom ?? method;
        if (method.Parameters.Any(static parameter => IsFormatProvider(parameter.Type)))
            return false;

        foreach (var overload in method.ContainingType.GetMembers(method.Name).OfType<IMethodSymbol>())
        {
            if (overload.Parameters.Length != method.Parameters.Length + 1 || overload.IsStatic != method.IsStatic
                || overload.Arity != method.Arity || overload.DeclaredAccessibility == Accessibility.Private
                || !overload.Parameters.Any(static parameter => IsFormatProvider(parameter.Type)))
                continue;

            var candidate = method.IsGenericMethod ? overload.Construct(method.TypeArguments.ToArray()) : overload;
            var provider = candidate.Parameters.First(parameter => IsFormatProvider(parameter.Type));
            var originalIndex = 0;
            foreach (var extra in candidate.Parameters)
            {
                if (extra.Ordinal == provider.Ordinal)
                    continue;

                var original = method.Parameters[originalIndex];
                if (extra.RefKind != original.RefKind || !SymbolEqualityComparer.Default.Equals(extra.Type, original.Type))
                    break;

                originalIndex++;
            }

            if (originalIndex == method.Parameters.Length)
                return true;
        }

        return false;
    }

    private static bool IsFormatProvider(ITypeSymbol type)
    {
        return MemberSyntax.IsType(type, "System", "IFormatProvider")
               || MemberSyntax.IsType(type, "System.Globalization", "CultureInfo");
    }

    private static bool IsCultureSensitiveStringComparison(IMethodSymbol method)
    {
        return method.ContainingType.SpecialType == SpecialType.System_String
               && method.Name is "Compare" or "CompareTo" or "StartsWith" or "EndsWith" or "IndexOf" or "LastIndexOf"
               && method.Parameters.Any(static parameter => parameter.Type.SpecialType == SpecialType.System_String)
               && !method.Parameters.Any(static parameter => IsFormatProvider(parameter.Type)
                   || MemberSyntax.IsType(parameter.Type, "System", "StringComparison"));
    }

    private static bool SortsStrings(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        var owner = method.ContainingType;
        var sortsStrings = method.Name switch
        {
            "Sort" or "BinarySearch" => SortsArrayOrList(invocation),
            _ when MemberSyntax.IsType(owner, "System.Linq", "Enumerable")
                   || MemberSyntax.IsType(owner, "System.Linq", "ImmutableArrayExtensions")
                   || MemberSyntax.IsType(owner, "System", "MemoryExtensions") => LinqUsesString(method),
            _ => false
        };

        return sortsStrings && !invocation.Arguments.Any(static argument =>
            argument.Parameter is { } parameter && IsComparer(parameter.Type) && !IsNullConstant(argument.Value));
    }

    private static bool LinqUsesString(IMethodSymbol method)
    {
        return method.Name switch
        {
            "Order" or "OrderDescending" => HasStringTypeArgument(method.TypeArguments),
            "Min" or "Max" => method.ReturnType.SpecialType == SpecialType.System_String,
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or "MinBy" or "MaxBy"
                => method.Parameters.Any(static parameter => parameter.Type is INamedTypeSymbol
                    { DelegateInvokeMethod.ReturnType.SpecialType: SpecialType.System_String }),
            _ => false
        };
    }

    private static bool SortsArrayOrList(IInvocationOperation invocation)
    {
        var owner = invocation.TargetMethod.ContainingType;
        if (HasStringTypeArgument(owner.TypeArguments)
            && (MemberSyntax.IsType(owner, "System.Collections.Generic", "List")
            || MemberSyntax.IsType(owner, "System.Collections.Immutable", "ImmutableList")
            || MemberSyntax.IsType(owner, "System.Collections.Immutable", "ImmutableArray")))
            return true;

        if (owner.SpecialType != SpecialType.System_Array
            && !MemberSyntax.IsType(owner, "System", "MemoryExtensions")
            && !MemberSyntax.IsType(owner, "System.Collections.Immutable", "ImmutableArray"))
            return false;

        var collection = invocation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Ordinal == 0);
        return collection is not null && (WrittenOperand(collection.Value).Type is IArrayTypeSymbol
            { ElementType.SpecialType: SpecialType.System_String }
            || collection.Parameter?.Type is INamedTypeSymbol sequence && HasStringTypeArgument(sequence.TypeArguments));
    }

    private static bool IsComparer(ITypeSymbol type)
    {
        return MemberSyntax.IsType(type, "System.Collections.Generic", "IComparer")
               || MemberSyntax.IsType(type, "System.Collections", "IComparer")
               || MemberSyntax.IsType(type, "System", "Comparison");
    }

    private static bool HasStringTypeArgument(ImmutableArray<ITypeSymbol> arguments)
    {
        return arguments.Length == 1 && arguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsNullConstant(IOperation operation)
    {
        return operation.ConstantValue is { HasValue: true, Value: null }
               || WrittenOperand(operation).ConstantValue is { HasValue: true, Value: null };
    }

    private static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var literal = (InterpolatedStringExpressionSyntax)context.Node;
        var info = context.SemanticModel.GetTypeInfo(literal, context.CancellationToken);
        var conversion = context.SemanticModel.GetConversion(literal, context.CancellationToken);
        if (info.ConvertedType?.SpecialType != SpecialType.System_String
            && (info.Type?.SpecialType != SpecialType.System_String
                || conversion.IsInterpolatedString || conversion.IsInterpolatedStringHandler))
            return;

        foreach (var hole in literal.Contents.OfType<InterpolationSyntax>())
            if (UsesCurrentCulture(context.SemanticModel.GetTypeInfo(hole.Expression, context.CancellationToken).Type))
                context.ReportDiagnostic(Diagnostic.Create(Rule, hole.Expression.GetLocation()));
    }

    private static void AnalyzeConcatenation(OperationAnalysisContext context)
    {
        if (context.Operation is not IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String, OperatorMethod: null,
                Syntax: BinaryExpressionSyntax syntax
            } concat || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        ReportOperand(context, concat.LeftOperand, syntax.Left);
        ReportOperand(context, concat.RightOperand, syntax.Right);
    }

    private static void ReportOperand(OperationAnalysisContext context, IOperation operand, ExpressionSyntax syntax)
    {
        if (UsesCurrentCulture(WrittenOperand(operand).Type))
            context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation()));
    }

    private static IOperation WrittenOperand(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;

        return operation;
    }

    private static bool UsesCurrentCulture(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        if (type is null || IsCultureFree(type))
            return false;

        return type is ITypeParameterSymbol parameter
            ? parameter.ConstraintTypes.Any(UsesCurrentCulture)
            : MemberSyntax.IsType(type, "System", "IFormattable")
              || type.AllInterfaces.Any(static candidate => MemberSyntax.IsType(candidate, "System", "IFormattable"));
    }

    private static bool IsCultureFree(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Char or SpecialType.System_Boolean or SpecialType.System_Enum
               || type.TypeKind == TypeKind.Enum
               || type is ITypeParameterSymbol parameter
               && parameter.ConstraintTypes.Any(static constraint => constraint.SpecialType == SpecialType.System_Enum)
               || MemberSyntax.IsType(type, "System", "Guid")
               || MemberSyntax.IsType(type, "System", "Version")
               || MemberSyntax.IsType(type, "System", "Uri")
               || MemberSyntax.IsType(type, "System.Text", "Rune");
    }
}
