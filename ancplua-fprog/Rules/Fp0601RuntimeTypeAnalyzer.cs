using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0601: inspects the runtime type of a value whose static type hides it, or writes dynamic.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0601RuntimeTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0601",
        "Do not inspect a runtime type hidden by the static type",
        "Use a statically typed value or pass in the operation to perform");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDynamicType, SyntaxKind.IdentifierName);
        context.RegisterOperationAction(Analyze,
            OperationKind.IsType, OperationKind.IsPattern, OperationKind.Conversion, OperationKind.TypeOf,
            OperationKind.Invocation, OperationKind.DynamicInvocation, OperationKind.DeclarationPattern,
            OperationKind.TypePattern, OperationKind.RecursivePattern);
    }

    private static void AnalyzeDynamicType(SyntaxNodeAnalysisContext context)
    {
        var name = (IdentifierNameSyntax)context.Node;
        if (name.Identifier.Text == "dynamic"
            && CoreScope.IsInCore(context.ContainingSymbol)
            && context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is IDynamicTypeSymbol)
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.Identifier.GetLocation()));
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol) ||
            context.Operation is { IsImplicit: true } and not IPatternOperation)
            return;

        if (FindTypeTest(context.Operation, context.Compilation) is { } location)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static Location? FindTypeTest(IOperation operation, Compilation compilation)
    {
        return operation switch
        {
            IIsTypeOperation { Syntax: BinaryExpressionSyntax syntax } typeTest
                when InspectsOperand(typeTest.ValueOperand, typeTest.TypeOperand, compilation)
                => syntax.OperatorToken.GetLocation(),
            IIsPatternOperation patternTest
                when patternTest.Pattern.DescendantsAndSelf().OfType<IPatternOperation>()
                    .Any(pattern => InspectsPatternInput(pattern, compilation))
                => patternTest.Syntax switch
                {
                    IsPatternExpressionSyntax syntax => syntax.IsKeyword.GetLocation(),
                    BinaryExpressionSyntax syntax => syntax.OperatorToken.GetLocation(),
                    _ => null
                },
            IConversionOperation { IsTryCast: true, Syntax: BinaryExpressionSyntax syntax } conversion
                when InspectsOperand(conversion.Operand, conversion.Type, compilation)
                => syntax.OperatorToken.GetLocation(),
            IConversionOperation { Syntax: CastExpressionSyntax syntax } conversion
                when IsRuntimeCast(conversion, compilation)
                => syntax.GetLocation(),
            ITypeOfOperation typeOf when UnwrapNullable(typeOf.TypeOperand) is ITypeParameterSymbol
                => typeOf.Syntax.GetLocation(),
            IInvocationOperation invocation when IsObjectGetType(invocation) &&
                                              IsHiddenOperand(invocation.Instance, compilation)
                => GetMethodNameLocation(invocation.Syntax),
            IDynamicInvocationOperation invocation when IsDynamicGetType(invocation)
                => GetMethodNameLocation(invocation.Syntax),
            IPatternOperation pattern when IsInSwitchPattern(pattern) && InspectsPatternInput(pattern, compilation)
                => pattern.Syntax.GetLocation(),
            _ => null
        };
    }

    private static bool InspectsOperand(IOperation value, ITypeSymbol? tested, Compilation compilation)
    {
        return IsHiddenOperand(value, compilation)
               && UnionModel.GetUnionOfExpression(value, compilation) is null
               && !IsNullCheck(WrittenOperand(value)?.Type, tested, compilation);
    }

    private static bool InspectsPatternInput(IPatternOperation pattern, Compilation compilation)
    {
        var testedType = pattern switch
        {
            IDeclarationPatternOperation { MatchesNull: false } declaration => declaration.MatchedType,
            ITypePatternOperation type => type.MatchedType,
            IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: not null } } recursive
                => recursive.MatchedType,
            _ => null
        };

        if (testedType is null || !IsHiddenType(pattern.InputType) || IsNullCheck(pattern.InputType, testedType, compilation))
            return false;

        // Logical patterns keep testing the same value. A positional or property subpattern has its own input.
        return GoverningPatternValue(pattern) is not { } value
               || (UnionModel.GetUnionOfExpression(value, compilation) is null && !IsContractParameter(value, compilation));
    }

    private static bool IsInSwitchPattern(IPatternOperation pattern)
    {
        IOperation current = pattern;
        while (current.Parent is IPatternOperation or IPropertySubpatternOperation)
            current = current.Parent;

        return current.Parent is ISwitchExpressionArmOperation or IPatternCaseClauseOperation;
    }

    private static IOperation? GoverningPatternValue(IPatternOperation pattern)
    {
        IOperation current = pattern;
        while (current.Parent is INegatedPatternOperation or IBinaryPatternOperation)
            current = current.Parent;

        return current.Parent switch
        {
            IIsPatternOperation isPattern => isPattern.Value,
            ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation expression } => expression.Value,
            IPatternCaseClauseOperation { Parent: ISwitchCaseOperation { Parent: ISwitchOperation statement } }
                => statement.Value,
            _ => null
        };
    }

    private static bool IsRuntimeCast(IConversionOperation conversion, Compilation compilation)
    {
        if (conversion.Operand.Type is not { } source || conversion.Type is not { } target
            || !IsHiddenOperand(conversion.Operand, compilation))
            return false;

        var kind = compilation.ClassifyConversion(source, target);
        return kind is { IsExplicit: true, IsReference: true } or { IsUnboxing: true }
               || (kind.IsDynamic && target.SpecialType != SpecialType.System_Object);
    }

    private static bool IsObjectGetType(IInvocationOperation invocation)
    {
        return invocation.TargetMethod is
        {
            Name: nameof(GetType),
            IsStatic: false,
            Parameters.IsEmpty: true,
            ContainingType.SpecialType: SpecialType.System_Object
        };
    }

    private static bool IsDynamicGetType(IDynamicInvocationOperation invocation)
    {
        return invocation is
        {
            Arguments.IsEmpty: true,
            Operation: IDynamicMemberReferenceOperation
            {
                MemberName: nameof(GetType),
                TypeArguments.IsEmpty: true,
                Instance.Type.TypeKind: TypeKind.Dynamic
            }
        };
    }

    private static Location? GetMethodNameLocation(SyntaxNode syntax)
    {
        return syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
                => access.Name.Identifier.GetLocation(),
            InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding }
                => binding.Name.Identifier.GetLocation(),
            _ => null
        };
    }

    private static bool IsHiddenOperand(IOperation? value, Compilation compilation)
    {
        return WrittenOperand(value) is { } written && IsHiddenType(written.Type)
               && !IsContractParameter(written, compilation);
    }

    private static bool IsHiddenType(ITypeSymbol? type)
    {
        return UnwrapNullable(type) is { } actual && UnionModel.GetUnionType(actual) is null &&
               actual is ITypeParameterSymbol or { SpecialType: SpecialType.System_Object } or { TypeKind: TypeKind.Dynamic };
    }

    private static bool IsNullCheck(ITypeSymbol? input, ITypeSymbol? tested, Compilation compilation)
    {
        return UnwrapNullable(input) is { } value && UnwrapNullable(tested) is { } target &&
               UnionModel.AlwaysConvertsTo(compilation, value, target);
    }

    private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
    }

    private static IOperation? WrittenOperand(IOperation? value)
    {
        while (true)
        {
            switch (value)
            {
                case IConversionOperation { IsImplicit: true, Conversion.IsUserDefined: false } conversion:
                    value = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    value = parenthesized.Operand;
                    continue;
                case IConditionalAccessInstanceOperation instance:
                    var access = instance.Parent;
                    while (access is not null && access is not IConditionalAccessOperation)
                        access = access.Parent;
                    value = (access as IConditionalAccessOperation)?.Operation;
                    continue;
                default:
                    return value;
            }
        }
    }

    private static bool IsContractParameter(IOperation value, Compilation compilation)
    {
        var original = UnionModel.ValueBeforeUpcast(value, compilation);
        if (original is not IParameterReferenceOperation
            { Parameter: { Type.SpecialType: SpecialType.System_Object } parameter })
            return false;

        var owner = parameter.ContainingSymbol;
        if (owner is IMethodSymbol { AssociatedSymbol: IPropertySymbol indexer })
            owner = indexer;

        if (ExplicitlyImplementsExternalMember(owner))
            return true;

        for (var ancestor = OverriddenMember(owner); ancestor is not null; ancestor = OverriddenMember(ancestor))
            if (!CoreScope.IsInCore(ancestor))
                return true;

        if (owner.ContainingType is not { } declaringType)
            return false;

        foreach (var contract in declaringType.AllInterfaces)
        {
            if (CoreScope.IsInCore(contract))
                continue;

            foreach (var member in contract.GetMembers(owner.Name))
                if (SymbolEqualityComparer.Default.Equals(owner, declaringType.FindImplementationForInterfaceMember(member)))
                    return true;
        }

        return false;
    }

    private static bool ExplicitlyImplementsExternalMember(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Any(member => !CoreScope.IsInCore(member)),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Any(member => !CoreScope.IsInCore(member)),
            _ => false
        };
    }

    private static ISymbol? OverriddenMember(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null
        };
    }
}
