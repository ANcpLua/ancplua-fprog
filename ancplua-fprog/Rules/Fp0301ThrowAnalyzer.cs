using System.Collections.Concurrent;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

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
        // Roslyn 5.9.0 runs a syntax node action inside the body of a C# 15 union declaration once per
        // enclosing declaration (its member, the union, the union's container), so each location is
        // reported only once per compilation.
        context.RegisterCompilationStartAction(static start =>
        {
            var reported = new ConcurrentDictionary<(SyntaxTree Tree, TextSpan Span), byte>();
            start.RegisterSyntaxNodeAction(
                node => Analyze(node, reported),
                SyntaxKind.ThrowStatement,
                SyntaxKind.ThrowExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<(SyntaxTree Tree, TextSpan Span), byte> reported)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var keyword = context.Node switch
        {
            ThrowStatementSyntax statement => statement.ThrowKeyword,
            ThrowExpressionSyntax expression => expression.ThrowKeyword,
            _ => default
        };

        if (keyword.IsKind(SyntaxKind.ThrowKeyword) && reported.TryAdd((context.Node.SyntaxTree, keyword.Span), 0))
            context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation()));
    }
}
