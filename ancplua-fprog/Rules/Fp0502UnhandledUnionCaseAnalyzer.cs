using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0502: a <c>switch</c> expression over a union value has a case that no arm without a <c>when</c>
///     clause names with a type test for the case's own type (<see cref="UnionSwitchCoverage.CasesNotNamed" />).
///     A case reached only through a catch-all, a base type or an arm with a <c>when</c> clause counts as
///     not handled, and so does a case that the code at the switch cannot name (a private nested case seen
///     from outside the union). Reported at the <c>switch</c> keyword. Applies inside and outside core; <c>switch</c>
///     statements are not checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0502UnhandledUnionCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0502",
        "Handle every case of a union in a switch expression",
        "The switch expression over '{0}' does not handle every case with an arm that has no 'when' clause: {1}");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var model = new UnionModel(start.Compilation);
            start.RegisterOperationAction(
                operationContext => Analyze(operationContext, model),
                OperationKind.SwitchExpression);
        });
    }

    private static void Analyze(OperationAnalysisContext context, UnionModel model)
    {
        var switchExpression = (ISwitchExpressionOperation)context.Operation;
        if (switchExpression.Syntax is not SwitchExpressionSyntax syntax
            || !model.TryGetCoverage(switchExpression, out var coverage))
            return;

        // A case that the code at the switch cannot name (a private nested case seen from outside the
        // union) is in none of the coverage lists, yet no arm handles it. GetCases still has it.
        var nameable = coverage.Cases;
        var missing = coverage.CasesNotNamed.AddRange(
            model.GetCases(switchExpression.Value.Type)
                .RemoveAll(@case => nameable.Contains(@case, SymbolEqualityComparer.Default)));
        if (missing.IsEmpty)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            syntax.SwitchKeyword.GetLocation(),
            coverage.Union.Name,
            string.Join(", ", missing.Select(static @case => @case.Name))));
    }
}