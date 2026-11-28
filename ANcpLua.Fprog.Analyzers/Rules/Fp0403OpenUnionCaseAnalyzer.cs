using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0403 requires each direct case of a closed class to be sealed or closed.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0403OpenUnionCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0403",
        "Declare a closed class case sealed or closed",
        "Declare case '{0}' of '{1}' sealed or closed");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type is { IsSealed: false, IsClosed: false, BaseType: { IsClosed: true } union }
            && CoreScope.IsDeclaredInSource(type)
            && type.Locations is { Length: > 0 } locations
            && locations[0].IsInSource)
            context.ReportDiagnostic(Diagnostic.Create(Rule, locations[0], type.Name, union.Name));
    }
}
