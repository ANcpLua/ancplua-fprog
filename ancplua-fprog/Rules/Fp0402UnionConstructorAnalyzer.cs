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

        var name = SourceName(type, context.CancellationToken);
        var hasDeclaredConstructor = false;
        foreach (var constructor in type.InstanceConstructors)
        {
            if (!CoreScope.IsDeclaredInSource(constructor))
                continue;

            hasDeclaredConstructor = true;
            if (constructor.DeclaredAccessibility == Accessibility.Private)
                continue;

            string fix;
            if (IsPrimaryConstructor(constructor, context.CancellationToken))
                fix = $"replace the primary constructor parameter list with a 'private {name}(...)' constructor that takes the same parameters";
            else if (IsRecordCopyConstructor(type, constructor))
                fix = "remove this explicit copy constructor (a record copy constructor cannot be private; the compiler generates one)";
            else
                fix = $"write 'private' instead of '{AccessibilityKeyword(constructor.DeclaredAccessibility)}' on this constructor";

            // A partial constructor has two declarations, each with its own name; the symbol of the
            // defining part carries only its own location.
            ReportAtName(context, constructor, name, fix);
            if (constructor.PartialImplementationPart is { } implementation)
                ReportAtName(context, implementation, name, fix);
        }

        if (!hasDeclaredConstructor)
        {
            var fix = type.IsStatic
                ? $"remove 'static' from the declaration and declare 'private {name}() {{ }}'"
                : $"declare 'private {name}() {{ }}'";
            ReportAtName(context, type, name, fix);
        }
    }

    private static string SourceName(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        // The identifier as written, so a verbatim name keeps its '@'.
        if (!type.Locations.IsEmpty && type.Locations[0] is { SourceTree: { } tree } location)
            return tree.GetText(cancellationToken).ToString(location.SourceSpan);

        return type.Name;
    }

    private static bool IsRecordCopyConstructor(INamedTypeSymbol type, IMethodSymbol constructor)
        => type.IsRecord
           && constructor.Parameters.Length == 1
           && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type.OriginalDefinition, type.OriginalDefinition);

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

    private static void ReportAtName(SymbolAnalysisContext context, ISymbol symbol, string union, string fix)
    {
        if (!symbol.Locations.IsEmpty)
            context.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations[0], union, fix));
    }
}
