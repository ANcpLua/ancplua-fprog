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
/// interface of the union, or <c>object</c>. Patterns on either side of a top-level <c>or</c> or
/// <c>and</c> count; subpatterns of a property or positional pattern do not. Reported at the pattern,
/// or at the <c>default</c> keyword. Applies inside and outside core.
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

    /// <summary>Reports every catch-all among <paramref name="pattern"/> and the sides of its top-level <c>or</c> and <c>and</c>.</summary>
    private static void ReportCatchAlls(OperationAnalysisContext context, IPatternOperation pattern, ITypeSymbol valueType)
    {
        if (pattern is IBinaryPatternOperation binary)
        {
            ReportCatchAlls(context, binary.LeftPattern, valueType);
            ReportCatchAlls(context, binary.RightPattern, valueType);
            return;
        }

        var syntax = GetPatternSyntax(pattern);
        if (syntax is VarPatternSyntax || IsCatchAll(context.Compilation, pattern, valueType))
            context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation()));
    }

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
    /// The pattern as written. Observed with Roslyn 5.9.0: the operation of <c>var _</c> and of
    /// <c>var (a, b)</c> has the designation as its syntax, so the enclosing <c>var</c> pattern is used.
    /// </summary>
    private static SyntaxNode GetPatternSyntax(IPatternOperation pattern)
    {
        var syntax = pattern.Syntax;
        while (syntax is ParenthesizedPatternSyntax parenthesized)
            syntax = parenthesized.Pattern;

        return syntax is VariableDesignationSyntax { Parent: VarPatternSyntax varPattern } ? varPattern : syntax;
    }
}
