using System.Collections.Immutable;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0402: a union has an instance constructor declared in source that is not <c>private</c>
/// (reported at the constructor name, at both declarations of a partial constructor; a primary
/// constructor at the union's name), or has no declared instance constructor, so the implicit one
/// is not private (reported once at the union's name).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0402UnionConstructorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0402",
        "Union constructors must be private",
        "Union '{0}' must have only private constructors: {1}, so that only its nested sealed cases can derive from it");

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
            if (constructor.DeclaredAccessibility == Accessibility.Private)
                continue;

            var fix = IsPrimaryConstructor(constructor, context.CancellationToken)
                ? $"replace the primary constructor parameter list with a 'private {type.Name}(...)' constructor that takes the same parameters"
                : $"write 'private' instead of '{AccessibilityKeyword(constructor.DeclaredAccessibility)}' on this constructor";

            // A partial constructor has two declarations, each with its own name; the symbol of the
            // defining part carries only its own location.
            ReportAtName(context, constructor, type, fix);
            if (constructor.PartialImplementationPart is { } implementation)
                ReportAtName(context, implementation, type, fix);
        }

        if (!hasDeclaredConstructor)
            ReportAtName(context, type, type, $"declare 'private {type.Name}() {{ }}'");
    }

    private static bool IsPrimaryConstructor(IMethodSymbol constructor, CancellationToken cancellationToken)
    {
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax)
                return true;
        }

        return false;
    }

    private static string AccessibilityKeyword(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "the current access modifier"
        };

    private static void ReportAtName(SymbolAnalysisContext context, ISymbol symbol, INamedTypeSymbol union, string fix)
    {
        if (!symbol.Locations.IsEmpty)
            context.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations[0], union.Name, fix));
    }
}
