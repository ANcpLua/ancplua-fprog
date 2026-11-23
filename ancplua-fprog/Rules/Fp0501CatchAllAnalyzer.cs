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
        "Replace this catch-all with one arm or label per case of the union, so that a case added later is reported instead of falling through");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var unions = new UnionModel(start.Compilation);
            start.RegisterOperationAction(operation => AnalyzeSwitchExpression(operation, unions), OperationKind.SwitchExpression);
            start.RegisterOperationAction(operation => AnalyzeSwitchStatement(operation, unions), OperationKind.Switch);
        });
    }

    private static void AnalyzeSwitchExpression(OperationAnalysisContext context, UnionModel unions)
    {
        var expression = (ISwitchExpressionOperation)context.Operation;
        foreach (var arm in expression.Arms)
            ReportCatchAlls(context, unions, expression.Value, arm.Pattern);
    }

    private static void AnalyzeSwitchStatement(OperationAnalysisContext context, UnionModel unions)
    {
        var statement = (ISwitchOperation)context.Operation;
        var cancellationToken = context.CancellationToken;
        foreach (var section in statement.Cases)
        foreach (var clause in section.Clauses)
            switch (clause)
            {
                case IDefaultCaseClauseOperation { Syntax: DefaultSwitchLabelSyntax label }
                    when unions.GetUnionOfExpression(statement.Value, cancellationToken) is not null
                         || unions.GetTupleWithUnionElement(statement.Value, cancellationToken) is not null:
                    context.ReportDiagnostic(Diagnostic.Create(Rule, label.Keyword.GetLocation()));
                    break;
                case IPatternCaseClauseOperation pattern:
                    ReportCatchAlls(context, unions, statement.Value, pattern.Pattern);
                    break;
            }
    }

    private static void ReportCatchAlls(
        OperationAnalysisContext context, UnionModel unions, IOperation value, IPatternOperation pattern)
    {
        foreach (var catchAll in unions.GetCatchAlls(value, pattern, context.CancellationToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, catchAll.GetLocation()));
    }
}
