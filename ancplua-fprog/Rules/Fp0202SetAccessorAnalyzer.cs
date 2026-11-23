using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0202: a property or indexer inside the functional core declares a <c>set</c> accessor, with or
///     without a body and with any accessibility. <c>init</c> accessors are allowed. Reported at the
///     <c>set</c> keyword; setters the compiler synthesizes have no keyword in source and are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0202SetAccessorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0202",
        "Do not declare set accessors in the functional core",
        "Use an init accessor or a get-only property instead of a set accessor");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SetAccessorDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // Roslyn 5.9.0 runs this action again for an accessor inside a C# 15 union body, once with the
        // union and once with its container as containing symbol. The accessor's own pass carries the
        // accessor method, so the passes of a type or namespace only repeat it.
        if (context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol)
            return;

        if (context.Node is not AccessorDeclarationSyntax accessor || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, accessor.Keyword.GetLocation()));
    }
}