using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0601: a runtime type test inside the functional core that involves an unconstrained type
///     parameter, either as the tested value or as the tested type: <c>is</c>, <c>as</c>, a type
///     pattern, <c>typeof(T)</c>, or <c>GetType()</c> on a value of type <c>T</c>. A type parameter is
///     unconstrained when no constraint names a type (<c>class</c>, <c>struct</c>, <c>notnull</c>,
///     <c>unmanaged</c> and <c>new()</c> name none), or when it is constrained only to other
///     unconstrained type parameters. A value of type <c>T</c> stays one through a cast such as
///     <c>(object)value</c>. Null checks are not type tests: <c>is null</c>, <c>is not null</c>,
///     <c>is { }</c>, <c>var</c> and discard patterns, and tests whose outcome the static types decide
///     except for null (<c>x is T v</c> for <c>x</c> of type <c>T?</c>, <c>x is object</c>). Constant and
///     relational patterns are no type tests, and neither is a C# 15 union value tested against one of
///     its own case types.
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
            OperationKind.RecursivePattern);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        // A type name as a case label (`case string:`) or tested against a union value (`u is T`)
        // is an implicit pattern over source syntax.
        if (context.Operation is { IsImplicit: true } and not IPatternOperation)
            return;

        var location = FindTypeTest(context.Operation, context.Compilation);
        if (location is not null && CoreScope.IsInCore(context.ContainingSymbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static Location? FindTypeTest(IOperation operation, Compilation compilation)
    {
        return operation switch
        {
            IIsTypeOperation { Syntax: BinaryExpressionSyntax binary } isType
                when IsTypeTest(isType.ValueOperand, isType.TypeOperand, compilation)
                => binary.OperatorToken.GetLocation(),
            IConversionOperation { IsTryCast: true, Syntax: BinaryExpressionSyntax binary } conversion
                when IsTypeTest(conversion.Operand, conversion.Type, compilation)
                => binary.OperatorToken.GetLocation(),
            ITypeOfOperation typeOf when IsOpaque(typeOf.TypeOperand)
                => typeOf.Syntax.GetLocation(),
            IInvocationOperation invocation when IsGetTypeOnOpaqueValue(invocation)
                => GetMethodNameLocation(invocation.Syntax),
            IPatternOperation { Syntax: BinaryExpressionSyntax binary } pattern
                when binary.IsKind(SyntaxKind.IsExpression) && IsTypeTestingPattern(pattern, compilation)
                => binary.OperatorToken.GetLocation(),
            IPatternOperation pattern when IsTypeTestingPattern(pattern, compilation)
                => pattern.Syntax.GetLocation(),
            _ => null
        };
    }

    private static bool IsTypeTest(IOperation value, ITypeSymbol? tested, Compilation compilation)
    {
        return (IsOpaqueValue(value) || IsOpaque(tested)) && !IsNullCheck(value.Type, tested, compilation);
    }

    private static bool IsTypeTestingPattern(IPatternOperation pattern, Compilation compilation)
    {
        return pattern switch
        {
            IDeclarationPatternOperation { MatchesNull: false } declaration
                => IsMatchedTypeTest(pattern, declaration.MatchedType, compilation),
            ITypePatternOperation type
                => IsMatchedTypeTest(pattern, type.MatchedType, compilation),
            IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: not null } } recursive
                => IsMatchedTypeTest(pattern, recursive.MatchedType, compilation),
            _ => false
        };
    }

    private static bool IsMatchedTypeTest(IPatternOperation pattern, ITypeSymbol? matched, Compilation compilation)
    {
        return (HasOpaqueInput(pattern) || IsOpaque(matched))
               && !IsNullCheck(pattern.InputType, matched, compilation)
               && !IsCaseOfUnion(pattern.InputType, matched);
    }

    /// <summary>
    ///     True when <paramref name="input" /> is a C# 15 union (it implements
    ///     <c>System.Runtime.CompilerServices.IUnion</c>) and <paramref name="matched" /> is one of its case
    ///     types, the parameter type of a public one-parameter constructor: the union's own discrimination.
    /// </summary>
    private static bool IsCaseOfUnion(ITypeSymbol? input, ITypeSymbol? matched)
    {
        if (input is not INamedTypeSymbol union || !union.AllInterfaces.Any(IsUnionInterface))
            return false;

        foreach (var constructor in union.InstanceConstructors)
            if (constructor is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 }
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, matched))
                return true;

        return false;
    }

    private static bool IsUnionInterface(INamedTypeSymbol type)
    {
        return type is
        {
            Name: "IUnion",
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "CompilerServices",
                ContainingNamespace:
                {
                    Name: "Runtime",
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                }
            }
        };
    }

    private static bool HasOpaqueInput(IPatternOperation pattern)
    {
        return IsOpaque(pattern.InputType) || IsOpaqueValue(GetTestedValue(pattern));
    }

    /// <summary>
    ///     The expression a pattern receives unnarrowed: the operand of <c>is</c> or the governing
    ///     expression of a switch, reached through <c>not</c>, <c>or</c> and the left side of <c>and</c>.
    /// </summary>
    private static IOperation? GetTestedValue(IPatternOperation pattern)
    {
        IOperation current = pattern;
        while (current.Parent is INegatedPatternOperation
               || (current.Parent is IBinaryPatternOperation binary
                   && (binary.OperatorKind == BinaryOperatorKind.Or || binary.LeftPattern == current)))
            current = current.Parent;

        return current.Parent switch
        {
            IIsPatternOperation isPattern => isPattern.Value,
            ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation switchExpression } => switchExpression
                .Value,
            IPatternCaseClauseOperation { Parent: ISwitchCaseOperation { Parent: ISwitchOperation switchStatement } }
                => switchStatement.Value,
            _ => null
        };
    }

    private static bool IsGetTypeOnOpaqueValue(IInvocationOperation invocation)
    {
        return invocation.TargetMethod is { Name: nameof(GetType), IsStatic: false, Parameters.IsEmpty: true }
               && IsOpaqueValue(invocation.Instance);
    }

    private static Location? GetMethodNameLocation(SyntaxNode syntax)
    {
        return syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name.Identifier
                .GetLocation(),
            InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } => binding.Name.Identifier
                .GetLocation(),
            _ => null
        };
    }

    /// <summary>
    ///     True when every non-null value of <paramref name="input" /> already is a <paramref name="tested" />
    ///     (identity, boxing, reference or nullable conversion, after unwrapping <c>Nullable&lt;T&gt;</c>),
    ///     so the test only checks for null.
    /// </summary>
    private static bool IsNullCheck(ITypeSymbol? input, ITypeSymbol? tested, Compilation compilation)
    {
        if (input is null || tested is null)
            return false;

        if (input is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            input = nullable.TypeArguments[0];

        var conversion = compilation.ClassifyCommonConversion(input, tested);
        return conversion is { IsImplicit: true, IsNumeric: false, IsUserDefined: false };
    }

    /// <summary>True for a value of an opaque type, also seen through conversions such as <c>(object)value</c>.</summary>
    private static bool IsOpaqueValue(IOperation? value)
    {
        for (; value is not null; value = (value as IConversionOperation)?.Operand)
            if (IsOpaque(value.Type))
                return true;

        return false;
    }

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
            if (constraint is not ITypeParameterSymbol other || !IsUnconstrained(other))
                return false;

        return true;
    }
}