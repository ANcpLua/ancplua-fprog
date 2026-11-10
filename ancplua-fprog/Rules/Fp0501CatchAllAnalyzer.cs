using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0501: a <c>switch</c> statement or <c>switch</c> expression over a union value uses a catch-all:
/// the discard <c>_</c>, a <c>var</c> pattern, <c>default:</c>, an empty property pattern <c>{ }</c>,
/// <c>not null</c>, or a type pattern (with or without a designation, with or without an empty property
/// pattern) whose type the switched value always converts to: the union itself, a base type, an
/// interface of the union, or <c>object</c>. Patterns on either side of a top-level <c>or</c> count;
/// patterns on either side of a top-level <c>and</c> count only when both sides are catch-alls, since
/// beside a case test they catch nothing beyond that case. Subpatterns of a property or positional
/// pattern do not count. Reported at the pattern, or at the <c>default</c> keyword. Applies inside and
/// outside core.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0501CatchAllAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0501",
        "Do not use a catch-all in a switch over a union",
        "Handle each case of the union explicitly instead of using a catch-all");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeSwitchExpression, OperationKind.SwitchExpression);
        context.RegisterOperationAction(AnalyzeSwitchStatement, OperationKind.Switch);
    }

    private static void AnalyzeSwitchExpression(OperationAnalysisContext context)
    {
        var switchExpression = (ISwitchExpressionOperation)context.Operation;
        if (GetUnionValueType(switchExpression.Value) is not { } valueType)
            return;

        foreach (var arm in switchExpression.Arms)
            ReportCatchAlls(context, arm.Pattern, valueType);
    }

    private static void AnalyzeSwitchStatement(OperationAnalysisContext context)
    {
        var switchStatement = (ISwitchOperation)context.Operation;
        if (GetUnionValueType(switchStatement.Value) is not { } valueType)
            return;

        foreach (var section in switchStatement.Cases)
        {
            foreach (var clause in section.Clauses)
            {
                switch (clause)
                {
                    case IDefaultCaseClauseOperation { Syntax: DefaultSwitchLabelSyntax label }:
                        context.ReportDiagnostic(Diagnostic.Create(Rule, label.Keyword.GetLocation()));
                        break;
                    case IPatternCaseClauseOperation patternClause:
                        ReportCatchAlls(context, patternClause.Pattern, valueType);
                        break;
                }
            }
        }
    }

    /// <summary>The static type of <paramref name="value"/> when it is a union value, otherwise null.</summary>
    private static ITypeSymbol? GetUnionValueType(IOperation value)
        => value.Type is { } type && UnionModel.GetUnionType(type) is not null ? type : null;

    /// <summary>
    /// Reports every catch-all among <paramref name="pattern"/> and the sides of its top-level <c>or</c>,
    /// and the sides of its top-level <c>and</c> when the whole <c>and</c> still matches every value.
    /// </summary>
    private static void ReportCatchAlls(OperationAnalysisContext context, IPatternOperation pattern, ITypeSymbol valueType)
    {
        if (pattern is IBinaryPatternOperation binary)
        {
            if (binary.OperatorKind == BinaryOperatorKind.And && !MatchesEveryValue(context.Compilation, binary, valueType))
                return;

            ReportCatchAlls(context, binary.LeftPattern, valueType);
            ReportCatchAlls(context, binary.RightPattern, valueType);
            return;
        }

        var syntax = GetPatternSyntax(pattern);
        if (syntax is VarPatternSyntax || IsCatchAll(context.Compilation, pattern, valueType))
            context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation()));
    }

    /// <summary>
    /// True when <paramref name="pattern"/> is a catch-all: a listed form, an <c>and</c> of two catch-alls,
    /// or an <c>or</c> with a catch-all on either side.
    /// </summary>
    private static bool MatchesEveryValue(Compilation compilation, IPatternOperation pattern, ITypeSymbol valueType)
        => pattern switch
        {
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } conjunction
                => MatchesEveryValue(compilation, conjunction.LeftPattern, valueType)
                   && MatchesEveryValue(compilation, conjunction.RightPattern, valueType),
            IBinaryPatternOperation disjunction
                => MatchesEveryValue(compilation, disjunction.LeftPattern, valueType)
                   || MatchesEveryValue(compilation, disjunction.RightPattern, valueType),
            _ => GetPatternSyntax(pattern) is VarPatternSyntax || IsCatchAll(compilation, pattern, valueType)
        };

    private static bool IsCatchAll(Compilation compilation, IPatternOperation pattern, ITypeSymbol valueType)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }: // var x
                return true;
            case IDeclarationPatternOperation { MatchedType: { } declaredType }:
                return AlwaysConvertsTo(compilation, valueType, declaredType);
            case ITypePatternOperation typePattern:
                return AlwaysConvertsTo(compilation, valueType, typePattern.MatchedType);
            case IRecursivePatternOperation { Syntax: RecursivePatternSyntax syntax } recursive:
                return syntax is { PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0 }
                       && (syntax.Type is null || AlwaysConvertsTo(compilation, valueType, recursive.MatchedType));
            case INegatedPatternOperation { Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } } }:
                return true;
            default:
                return false;
        }
    }

    /// <summary>True when every non-null value of <paramref name="valueType"/> is a <paramref name="testedType"/>.</summary>
    private static bool AlwaysConvertsTo(Compilation compilation, ITypeSymbol valueType, ITypeSymbol testedType)
    {
        var conversion = compilation.ClassifyConversion(valueType, testedType);
        return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing));
    }

    /// <summary>
    /// The pattern as written, without its parentheses. Observed with Roslyn 5.9.0: the operation of
    /// <c>var _</c> and of <c>var (a, b)</c> has the designation as its syntax, so the enclosing
    /// <c>var</c> pattern is used; in a <c>case (Shape):</c> label and in a <c>((Shape)) =&gt;</c> arm the
    /// type pattern's syntax is a parenthesized expression (inside a constant pattern), not a
    /// parenthesized pattern. A constant pattern has the span of its expression, so it is unwrapped too.
    /// </summary>
    private static SyntaxNode GetPatternSyntax(IPatternOperation pattern)
    {
        var syntax = pattern.Syntax;
        while (Unparenthesize(syntax) is { } inner)
            syntax = inner;

        return syntax is VariableDesignationSyntax { Parent: VarPatternSyntax varPattern } ? varPattern : syntax;
    }

    private static SyntaxNode? Unparenthesize(SyntaxNode syntax)
        => syntax switch
        {
            ParenthesizedPatternSyntax parenthesized => parenthesized.Pattern,
            ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
            ConstantPatternSyntax constant => constant.Expression,
            _ => null
        };
}
