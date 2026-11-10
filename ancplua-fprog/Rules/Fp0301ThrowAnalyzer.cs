using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0301: a <c>throw</c> statement or <c>throw</c> expression inside the functional core, including
/// a rethrow (<c>throw;</c>). Reported at the <c>throw</c> keyword.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0301ThrowAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0301",
        "Do not throw in the functional core",
        "Return an error value instead of throwing");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var keyword = context.Node switch
        {
            ThrowStatementSyntax statement => statement.ThrowKeyword,
            ThrowExpressionSyntax expression => expression.ThrowKeyword,
            _ => default
        };

        if (keyword.IsKind(SyntaxKind.ThrowKeyword))
            context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation()));
    }
}
