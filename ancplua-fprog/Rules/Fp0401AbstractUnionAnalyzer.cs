using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0401: a union (a class or record annotated <c>[Fprog.Union]</c>) that is not abstract, inside or
/// outside the functional core. Reported once, at the union's name in its first declaration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0401AbstractUnionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0401",
        "A union must be abstract",
        "Union '{0}' is not abstract; declare it as '{1} {0}'{2}, so that only its sealed nested cases can be instantiated");

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

            var first = FindDeclaration(type, location, context.CancellationToken);
            if (first is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, location, type.Name, type.IsRecord ? "abstract record" : "abstract class", ""));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                QuotedName(first),
                Declaration(first),
                Removal(type, first, context.CancellationToken)));
            return;
        }
    }

    private static TypeDeclarationSyntax? FindDeclaration(
        INamedTypeSymbol type,
        Location location,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == location.SourceTree
                && reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration
                && declaration.Identifier.Span == location.SourceSpan)
            {
                return declaration;
            }
        }

        return null;
    }

    // The name as written: '@' of a verbatim identifier kept, type parameters listed by name.
    private static string QuotedName(TypeDeclarationSyntax declaration)
    {
        if (declaration.TypeParameterList is not { Parameters.Count: > 0 } parameters)
            return declaration.Identifier.Text;

        var builder = new StringBuilder(declaration.Identifier.Text).Append('<');
        for (var i = 0; i < parameters.Parameters.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");
            builder.Append(parameters.Parameters[i].Identifier.Text);
        }

        return builder.Append('>').ToString();
    }

    // The declaration to write instead, keeping 'partial' and the kind of the union.
    private static string Declaration(TypeDeclarationSyntax declaration)
    {
        var partial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ? "partial " : "";
        var kind = declaration is RecordDeclarationSyntax ? "record" : "class";
        return "abstract " + partial + kind;
    }

    // A static or sealed type cannot also be abstract, so the message names the modifier to delete and,
    // when another partial part carries it, where that part is.
    private static string Removal(
        INamedTypeSymbol type,
        TypeDeclarationSyntax first,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!type.IsStatic && !type.IsSealed)
            return "";

        var modifier = type.IsStatic ? SyntaxKind.StaticKeyword : SyntaxKind.SealedKeyword;
        var text = type.IsStatic ? "static" : "sealed";
        if (first.Modifiers.Any(modifier))
            return " and remove '" + text + "'";

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax part || !part.Modifiers.Any(modifier))
                continue;

            if (part.SyntaxTree != first.SyntaxTree)
                return " and remove '" + text + "' from its other partial declaration";

            var line = part.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return " and remove '" + text + "' from its declaration on line "
                   + line.ToString(CultureInfo.InvariantCulture);
        }

        return " and remove '" + text + "'";
    }
}
