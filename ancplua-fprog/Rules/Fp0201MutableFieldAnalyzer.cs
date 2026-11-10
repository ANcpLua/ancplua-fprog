using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0201: a field inside the functional core that is neither <c>readonly</c> nor <c>const</c>, reported at
/// each variable declarator name. Static, struct, <c>volatile</c>, fixed-size buffer and <c>ref readonly</c>
/// ref fields count. Compiler-generated backing fields (including the one behind a field-like event) and
/// enum members (constants) are exempt.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0201MutableFieldAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0201",
        "Do not declare mutable fields in the functional core",
        "Make field '{0}' readonly or const");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (context.Symbol is IFieldSymbol { IsConst: false, IsReadOnly: false } field)
            ReportIfInCore(context, field);
    }

    private static void ReportIfInCore(SymbolAnalysisContext context, ISymbol symbol)
    {
        if (!CoreScope.IsDeclaredInSource(symbol) || !CoreScope.IsInCore(symbol))
            return;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax declarator)
                context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.Identifier.GetLocation(), symbol.Name));
        }
    }
}
