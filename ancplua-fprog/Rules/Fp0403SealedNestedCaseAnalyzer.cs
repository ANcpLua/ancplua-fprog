using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0403: a case of a union (a type whose direct base type is a union) is not sealed, or its
///     containing type is not that union. Applies to every union, inside and outside core. Reported once
///     per case at the case's name, for a partial case at its first declaration in source order.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0403SealedNestedCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0403",
        "A union case must be sealed and nested inside its union",
        "Make case '{0}' sealed and declare it inside union '{1}'");

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
        if (UnionModel.GetUnionOfCase(type) is not { } union)
            return;

        var nested = SymbolEqualityComparer.Default.Equals(type.ContainingType?.OriginalDefinition, union);
        if (type.IsSealed && nested)
            return;

        // The locations of a source type are the identifiers of its declarations in source order, one
        // per partial part. "Not sealed" and "not nested" are facts of the merged type: report it once.
        if (type.Locations is { Length: > 0 } locations && locations[0].IsInSource)
            context.ReportDiagnostic(Diagnostic.Create(Rule, locations[0], type.Name, union.Name));
    }
}