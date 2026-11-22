using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0502: a <c>switch</c> expression over a union value has a case without its own arm: an arm without a
/// <c>when</c> clause that names the case and matches every value of it (<see cref="UnionSwitchCoverage.CasesWithoutOwnArm"/>,
/// the same definition FPS001 uses). A case reached only through a catch-all, a base type, an arm with a
/// <c>when</c> clause or arms whose subpatterns reject some of its values (<c>Circle { Radius: &gt; 0 }</c>)
/// counts as not handled, and so does a case that the code at the switch cannot name (a private nested case
/// seen from outside the union). Reported at the <c>switch</c> keyword. Applies inside and outside core; <c>switch</c>
/// statements are not checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0502UnhandledUnionCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0502",
        "Handle every case of a union in a switch expression",
        "Add one arm without a 'when' clause per unhandled case of '{0}' that matches every value of it, for example '{1} => ...' or '{1}(var x) => ...', instead of a catch-all, a 'when' clause or a subpattern that rejects values: {2}");

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
        var missing = coverage.CasesWithoutOwnArm.AddRange(
            model.GetCases(switchExpression.Value.Type)
                .RemoveAll(@case => nameable.Contains(@case, SymbolEqualityComparer.Default)));
        if (missing.IsEmpty)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            syntax.SwitchKeyword.GetLocation(),
            coverage.Union.Name,
            NameOf(missing[0]),
            string.Join(", ", missing.Select(NameOf))));
    }

    /// <summary>The case as an arm names it: <c>Shape.Circle</c> for a nested case, <c>Circle</c> otherwise.</summary>
    private static string NameOf(INamedTypeSymbol @case)
        => @case.ContainingType is { } container ? container.Name + "." + @case.Name : @case.Name;
}
