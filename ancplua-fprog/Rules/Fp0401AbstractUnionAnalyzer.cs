using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0401: a union (a class or record annotated <c>[Fprog.Union]</c>) that is not abstract, inside or
///     outside the functional core. Reported once, at the union's name in its first declaration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0401AbstractUnionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0401",
        "A union must be abstract",
        "Union '{0}' is not abstract; declare it abstract so only its cases can be instantiated");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsAbstract || !UnionModel.IsUnion(type) || !CoreScope.IsDeclaredInSource(type))
            return;

        // The locations of a source type are the identifiers of its declarations in source order, one per
        // partial part. "Not abstract" is a fact of the merged type, so it is reported once, at the first.
        foreach (var location in type.Locations)
        {
            if (!location.IsInSource)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
            return;
        }
    }
}