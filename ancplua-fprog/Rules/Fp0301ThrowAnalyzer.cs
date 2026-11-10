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
        // Roslyn 5.9.0 runs a syntax node action inside the body of a C# 15 union declaration once with
        // the enclosing member, then again with the union type and with each declaration around it (a
        // type or a namespace). A throw is always executable code, so its genuine pass carries a member:
        // a method, accessor, constructor (also the primary constructor for a base argument), operator,
        // the field, property or event of an initializer, or the entry point of top-level statements.
        // A type or namespace as containing symbol is therefore one of the repeated passes.
        if (context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol
            || !CoreScope.IsInCore(context.ContainingSymbol))
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
