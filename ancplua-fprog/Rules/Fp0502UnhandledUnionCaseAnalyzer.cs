using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0502 requires an unguarded label for each case of a union in a switch statement.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0502UnhandledUnionCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0502",
        "Handle every case of a union in a switch statement",
        "Add unguarded labels to handle every case of '{0}': {1}");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var unions = new UnionModel(start.Compilation);
            start.RegisterOperationAction(operation => Analyze(operation, unions), OperationKind.Switch);
        });
    }

    private static void Analyze(OperationAnalysisContext context, UnionModel unions)
    {
        var statement = (ISwitchOperation)context.Operation;
        var cancellationToken = context.CancellationToken;
        if (statement.Syntax is not SwitchStatementSyntax syntax
            || unions.GetUnionOfExpression(statement.Value, cancellationToken) is not { } union)
            return;

        var patterns = ImmutableArray.CreateBuilder<IPatternOperation>();
        foreach (var section in statement.Cases)
        foreach (var clause in section.Clauses)
        {
            if (clause is IDefaultCaseClauseOperation)
                return;

            if (clause is not IPatternCaseClauseOperation label)
                continue;

            if (unions.CatchesSwitchedValue(statement.Value, label.Pattern, cancellationToken))
                return;

            if (label.Guard is null)
                patterns.Add(label.Pattern);
        }

        var missing = unions.GetCases(union, cancellationToken).Leaves
            .Where(@case => !patterns.Any(pattern => unions.CoversCase(pattern, @case)))
            .Select(@case => @case.Name)
            .ToArray();
        if (missing.Length > 0)
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, syntax.SwitchKeyword.GetLocation(), union.Type.Name, string.Join(", ", missing)));
    }
}
