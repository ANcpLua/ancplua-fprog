using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0401 closes inheritance in the functional core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0401OpenCoreClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0401",
        "Declare a core class sealed, static or closed",
        "Declare '{0}' sealed or static, or closed when its subtypes are the cases of a union");

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
        if (type is
            {
                TypeKind: TypeKind.Class, IsSealed: false, IsStatic: false, IsClosed: false,
                BaseType: not { IsClosed: true }
            }
            && CoreScope.IsInCore(type)
            && CoreScope.IsDeclaredInSource(type)
            && type.Locations is { Length: > 0 } locations
            && locations[0].IsInSource)
            context.ReportDiagnostic(Diagnostic.Create(Rule, locations[0], type.Name));
    }
}
