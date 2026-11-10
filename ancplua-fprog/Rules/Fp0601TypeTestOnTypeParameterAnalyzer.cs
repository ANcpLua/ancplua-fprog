using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0601: a runtime type test inside the functional core that involves an unconstrained type
/// parameter, either as the tested value or as the tested type: <c>is</c>, <c>as</c>, a type
/// pattern, <c>typeof(T)</c>, or <c>GetType()</c> on a value of type <c>T</c>. A type parameter is
/// unconstrained when no constraint names a type (<c>class</c>, <c>struct</c>, <c>notnull</c>,
/// <c>unmanaged</c> and <c>new()</c> name none), or when it is constrained only to other
/// unconstrained type parameters. Null checks (<c>is null</c>, <c>is not null</c>, <c>is { }</c>),
/// <c>var</c> and discard patterns are not type tests.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0601TypeTestOnTypeParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0601",
        "Do not inspect the runtime type of an unconstrained type parameter",
        "Treat the type parameter as opaque; add a constraint or pass a function instead of testing its type");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            Analyze,
            OperationKind.IsType,
            OperationKind.Conversion,
            OperationKind.TypeOf,
            OperationKind.Invocation,
            OperationKind.DeclarationPattern,
            OperationKind.TypePattern,
            OperationKind.RecursivePattern,
            OperationKind.ConstantPattern,
            OperationKind.RelationalPattern);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (context.Operation.IsImplicit)
            return;

        var location = FindTypeTest(context.Operation);
        if (location is not null && CoreScope.IsInCore(context.ContainingSymbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static Location? FindTypeTest(IOperation operation) => operation switch
    {
        IIsTypeOperation { Syntax: BinaryExpressionSyntax binary } isType
            when IsOpaque(isType.ValueOperand.Type) || IsOpaque(isType.TypeOperand)
            => binary.OperatorToken.GetLocation(),
        IConversionOperation { IsTryCast: true, Syntax: BinaryExpressionSyntax binary } conversion
            when IsOpaque(conversion.Operand.Type) || IsOpaque(conversion.Type)
            => binary.OperatorToken.GetLocation(),
        ITypeOfOperation typeOf when IsOpaque(typeOf.TypeOperand)
            => typeOf.Syntax.GetLocation(),
        IInvocationOperation invocation when IsGetTypeOnOpaqueValue(invocation)
            => GetMethodNameLocation(invocation.Syntax),
        IPatternOperation pattern when IsTypeTestingPattern(pattern)
            => pattern.Syntax.GetLocation(),
        _ => null
    };

    private static bool IsTypeTestingPattern(IPatternOperation pattern) => pattern switch
    {
        IDeclarationPatternOperation { MatchesNull: false } declaration
            => IsOpaque(pattern.InputType) || IsOpaque(declaration.MatchedType),
        ITypePatternOperation type
            => IsOpaque(pattern.InputType) || IsOpaque(type.MatchedType),
        IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: not null } } recursive
            => IsOpaque(pattern.InputType) || IsOpaque(recursive.MatchedType),
        IConstantPatternOperation constant
            => IsOpaque(pattern.InputType) && !IsNullConstant(constant.Value),
        IRelationalPatternOperation
            => IsOpaque(pattern.InputType),
        _ => false
    };

    private static bool IsNullConstant(IOperation value)
        => value.ConstantValue is { HasValue: true, Value: null };

    private static bool IsGetTypeOnOpaqueValue(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod is not { Name: nameof(GetType), IsStatic: false, Parameters.IsEmpty: true })
            return false;

        var instance = invocation.Instance;
        while (instance is IConversionOperation { IsImplicit: true } conversion)
            instance = conversion.Operand;

        return IsOpaque(instance?.Type);
    }

    private static Location? GetMethodNameLocation(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } => binding.Name.Identifier.GetLocation(),
        _ => null
    };

    /// <summary>True for an unconstrained type parameter, also when wrapped in <c>Nullable&lt;T&gt;</c>.</summary>
    private static bool IsOpaque(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        return type is ITypeParameterSymbol parameter && IsUnconstrained(parameter);
    }

    private static bool IsUnconstrained(ITypeParameterSymbol parameter)
    {
        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is not ITypeParameterSymbol other || !IsUnconstrained(other))
                return false;
        }

        return true;
    }
}
