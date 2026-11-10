using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0402: a union has an instance constructor declared in source that is not <c>private</c>
/// (reported at the constructor name; a primary constructor at the union's name), or has no declared
/// instance constructor, so the implicit one is not private (reported once at the union's name).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0402UnionConstructorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0402",
        "Union constructors must be private",
        "Declare a private constructor so that only the nested cases can derive from the union");

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
        if (!UnionModel.IsUnion(type) || !CoreScope.IsDeclaredInSource(type))
            return;

        var hasDeclaredConstructor = false;
        foreach (var constructor in type.InstanceConstructors)
        {
            if (!CoreScope.IsDeclaredInSource(constructor))
                continue;

            hasDeclaredConstructor = true;
            if (constructor.DeclaredAccessibility != Accessibility.Private && !constructor.Locations.IsEmpty)
                context.ReportDiagnostic(Diagnostic.Create(Rule, constructor.Locations[0]));
        }

        if (!hasDeclaredConstructor && !type.Locations.IsEmpty)
            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0]));
    }
}
