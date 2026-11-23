using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
        "Work through the static type instead of testing the runtime type: add a constraint and call its member, take a function parameter, or model the alternatives as a union");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDynamicType, SyntaxKind.IdentifierName);
        context.RegisterCompilationStartAction(start =>
        {
            var unions = new UnionModel(start.Compilation);
            start.RegisterOperationAction(operation => Analyze(operation, unions),
                OperationKind.IsType, OperationKind.IsPattern, OperationKind.Conversion, OperationKind.TypeOf,
                OperationKind.Invocation, OperationKind.DynamicInvocation, OperationKind.DeclarationPattern,
                OperationKind.TypePattern, OperationKind.RecursivePattern);
        });
    }

    private static void AnalyzeDynamicType(SyntaxNodeAnalysisContext context)
    {
        var name = (IdentifierNameSyntax)context.Node;
        if (name.Identifier.Text == "dynamic"
            && CoreScope.IsInCore(context.ContainingSymbol)
            && context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is IDynamicTypeSymbol)
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.Identifier.GetLocation()));
    }

    private static void Analyze(OperationAnalysisContext context, UnionModel unions)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol) ||
            context.Operation is { IsImplicit: true } and not IPatternOperation)
            return;

        if (FindTypeTest(context, unions) is { } location)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static Location? FindTypeTest(OperationAnalysisContext context, UnionModel unions)
    {
        var cancellationToken = context.CancellationToken;
        return context.Operation switch
        {
            IIsTypeOperation { Syntax: BinaryExpressionSyntax syntax } typeTest
                when InspectsOperand(typeTest.ValueOperand, typeTest.TypeOperand, unions, cancellationToken)
                => syntax.OperatorToken.GetLocation(),
            IIsPatternOperation patternTest
                when patternTest.Pattern.DescendantsAndSelf().OfType<IPatternOperation>()
                    .Any(pattern => InspectsPatternInput(pattern, unions, cancellationToken))
                => patternTest.Syntax switch
                {
                    IsPatternExpressionSyntax syntax => syntax.IsKeyword.GetLocation(),
                    BinaryExpressionSyntax syntax => syntax.OperatorToken.GetLocation(),
                    _ => null
                },
            IConversionOperation { IsTryCast: true, Syntax: BinaryExpressionSyntax syntax } conversion
                when InspectsOperand(conversion.Operand, conversion.Type, unions, cancellationToken)
                => syntax.OperatorToken.GetLocation(),
            IConversionOperation { Syntax: CastExpressionSyntax syntax } conversion
                when IsRuntimeCast(conversion, context, unions)
                => syntax.GetLocation(),
            ITypeOfOperation typeOf when UnwrapNullable(typeOf.TypeOperand) is ITypeParameterSymbol
                => typeOf.Syntax.GetLocation(),
            IInvocationOperation invocation when IsObjectGetType(invocation) &&
                                              IsHiddenOperand(invocation.Instance, unions, cancellationToken)
                => GetMethodNameLocation(invocation.Syntax),
            IDynamicInvocationOperation invocation when IsDynamicGetType(invocation)
                => GetMethodNameLocation(invocation.Syntax),
            IPatternOperation pattern when IsInSwitchPattern(pattern) && InspectsPatternInput(pattern, unions, cancellationToken)
                => pattern.Syntax.GetLocation(),
            _ => null
        };
    }

    private static bool InspectsOperand(
        IOperation value, ITypeSymbol? tested, UnionModel unions, CancellationToken cancellationToken)
    {
        return IsHiddenOperand(value, unions, cancellationToken)
               && unions.GetUnionOfExpression(value, cancellationToken) is null
               && !IsNullCheck(WrittenOperand(value)?.Type, tested, unions);
    }

    private static bool InspectsPatternInput(
        IPatternOperation pattern, UnionModel unions, CancellationToken cancellationToken)
    {
        var testedType = pattern switch
        {
            IDeclarationPatternOperation { MatchesNull: false } declaration => declaration.MatchedType,
            ITypePatternOperation type => type.MatchedType,
            IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: not null } } recursive
                => recursive.MatchedType,
            _ => null
        };

        if (testedType is null || !IsHiddenType(pattern.InputType, unions, cancellationToken)
                               || IsNullCheck(pattern.InputType, testedType, unions))
            return false;

        // Logical patterns keep testing the same value. A positional or property subpattern has its own input.
        return GoverningPatternValue(pattern) is not { } value
               || (unions.GetUnionOfExpression(value, cancellationToken) is null && !IsContractParameter(value, unions));
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

    private static bool IsRuntimeCast(IConversionOperation conversion, OperationAnalysisContext context, UnionModel unions)
    {
        if (conversion.Operand.Type is not { } source || conversion.Type is not { } target
            || !IsHiddenOperand(conversion.Operand, unions, context.CancellationToken))
            return false;

        var kind = context.Compilation.ClassifyConversion(source, target);
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

    private static bool IsHiddenOperand(IOperation? value, UnionModel unions, CancellationToken cancellationToken)
    {
        return WrittenOperand(value) is { } written && IsHiddenType(written.Type, unions, cancellationToken)
               && !IsContractParameter(written, unions);
    }

    private static bool IsHiddenType(ITypeSymbol? type, UnionModel unions, CancellationToken cancellationToken)
    {
        return UnwrapNullable(type) is (ITypeParameterSymbol
                   or { SpecialType: SpecialType.System_Object }
                   or { TypeKind: TypeKind.Dynamic }) and { } actual
               && unions.GetUnionType(actual, cancellationToken) is null;
    }

    private static bool IsNullCheck(ITypeSymbol? input, ITypeSymbol? tested, UnionModel unions)
    {
        return UnwrapNullable(input) is { } value && UnwrapNullable(tested) is { } target &&
               unions.AlwaysConvertsTo(value, target);
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

    private static bool IsContractParameter(IOperation value, UnionModel unions)
    {
        var original = unions.ValueBeforeUpcast(value);
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
