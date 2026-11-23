using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0501 reports catch-alls at switch pattern positions whose input is a union value.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0501CatchAllAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0501",
        "Do not use a catch-all in a switch over a union",
        "Handle each case of the union explicitly instead of using a catch-all");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeSwitchExpression, OperationKind.SwitchExpression);
        context.RegisterOperationAction(AnalyzeSwitchStatement, OperationKind.Switch);
    }

    private static void AnalyzeSwitchExpression(OperationAnalysisContext context)
    {
        var expression = (ISwitchExpressionOperation)context.Operation;
        foreach (var arm in expression.Arms)
            ReportCatchAlls(context, expression.Value, arm.Pattern);
    }

    private static void AnalyzeSwitchStatement(OperationAnalysisContext context)
    {
        var statement = (ISwitchOperation)context.Operation;
        foreach (var section in statement.Cases)
        foreach (var clause in section.Clauses)
            switch (clause)
            {
                case IDefaultCaseClauseOperation { Syntax: DefaultSwitchLabelSyntax label }
                    when UnionModel.GetUnionOfExpression(statement.Value, context.Compilation) is not null
                         || UnionModel.GetTupleWithUnionElement(statement.Value, context.Compilation) is not null:
                    context.ReportDiagnostic(Diagnostic.Create(Rule, label.Keyword.GetLocation()));
                    break;
                case IPatternCaseClauseOperation pattern:
                    ReportCatchAlls(context, statement.Value, pattern.Pattern);
                    break;
            }
    }

    private static void ReportCatchAlls(OperationAnalysisContext context, IOperation value, IPatternOperation pattern)
    {
        foreach (var catchAll in UnionModel.GetCatchAlls(context.Compilation, value, pattern, context.CancellationToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, catchAll.GetLocation()));
    }
}
