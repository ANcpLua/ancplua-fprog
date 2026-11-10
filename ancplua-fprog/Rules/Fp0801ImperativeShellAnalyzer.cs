using System.Collections.Concurrent;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0801: the functional core references the imperative shell. Reported at every written name token
/// of a type in <c>System.IO</c>, <c>System.Net</c> or <c>System.Data</c> (and the namespaces below them),
/// of <c>System.Console</c>, <c>System.Diagnostics.Process</c>, or one of the threading types
/// <c>Thread</c>, <c>Monitor</c>, <c>Lock</c>, <c>Mutex</c>, <c>Semaphore</c>, <c>SemaphoreSlim</c>,
/// <c>Interlocked</c> and <c>Timer</c> in <c>System.Threading</c> (their nested types, such as
/// <c>Lock.Scope</c>, are not listed), and at the <c>lock</c> keyword. Only names that bind to a type are
/// reported: a member reached without a type name token (<c>using static</c>, inherited statics) is not.
/// <c>using</c> directives and <c>var</c> are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0801ImperativeShellAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0801",
        "Do not reach the imperative shell from the functional core",
        "'{0}' belongs to the imperative shell (I/O, processes, threads or locks)");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // With Roslyn 5.9.0 a node inside the body of a C# 15 union declaration is analyzed up to three times
            // (for its member, for the union and for the union's container). Report every location once.
            var reported = new ConcurrentDictionary<Location, byte>();
            start.RegisterSyntaxNodeAction(
                node => AnalyzeName(node, reported), SyntaxKind.IdentifierName, SyntaxKind.GenericName);
            start.RegisterSyntaxNodeAction(node => AnalyzeLock(node, reported), SyntaxKind.LockStatement);
        });
    }

    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, ConcurrentDictionary<Location, byte> reported)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var keyword = ((LockStatementSyntax)context.Node).LockKeyword;
        Report(context, reported, keyword.GetLocation(), keyword.ValueText);
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context, ConcurrentDictionary<Location, byte> reported)
    {
        var name = (SimpleNameSyntax)context.Node;
        if (name.IsVar
            || name.IsPartOfStructuredTrivia()
            || !CoreScope.IsInCore(context.ContainingSymbol)
            || name.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is { } symbol
            && ReferencedType(symbol, name) is { } type
            && IsForbidden(type))
        {
            Report(context, reported, name.Identifier.GetLocation(), type.Name);
        }
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<Location, byte> reported,
        Location location,
        string subject)
    {
        // A source location is equal to another when both have the same syntax tree and span.
        if (reported.TryAdd(location, 0))
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, subject));
    }

    /// <summary>The type that <paramref name="name"/> names (for an attribute name, the attribute type).</summary>
    private static INamedTypeSymbol? ReferencedType(ISymbol symbol, SimpleNameSyntax name)
        => symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor when IsAttributeName(name)
                => constructor.ContainingType,
            _ => null
        };

    /// <summary>True when <paramref name="name"/> is the type name of an attribute (possibly qualified).</summary>
    private static bool IsAttributeName(SimpleNameSyntax name)
    {
        SyntaxNode current = name;
        while ((current.Parent is QualifiedNameSyntax qualified && qualified.Right == current)
               || (current.Parent is AliasQualifiedNameSyntax aliasQualified && aliasQualified.Name == current))
        {
            current = current.Parent;
        }

        return current.Parent is AttributeSyntax attribute && attribute.Name == current;
    }

    private static bool IsForbidden(INamedTypeSymbol type)
    {
        // For a nested type this is the namespace of its outermost containing type.
        var ns = type.ContainingNamespace;
        if (IsWithinSystemChild(ns, "IO") || IsWithinSystemChild(ns, "Net") || IsWithinSystemChild(ns, "Data"))
            return true;

        // The row lists the types below by name; a type nested in one of them (Lock.Scope) is not listed.
        if (type.ContainingType is not null)
            return false;

        return ns switch
        {
            _ when IsSystem(ns) => type.MetadataName == "Console",
            { Name: "Diagnostics" } when IsSystem(ns.ContainingNamespace) => type.MetadataName == "Process",
            { Name: "Threading" } when IsSystem(ns.ContainingNamespace)
                => type.MetadataName is "Thread" or "Monitor" or "Lock" or "Mutex" or "Semaphore"
                    or "SemaphoreSlim" or "Interlocked" or "Timer",
            _ => false
        };
    }

    /// <summary>True when <paramref name="ns"/> is <c>System.</c><paramref name="segment"/> or a namespace below it.</summary>
    private static bool IsWithinSystemChild(INamespaceSymbol? ns, string segment)
    {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            if (current.Name == segment && IsSystem(current.ContainingNamespace))
                return true;
        }

        return false;
    }

    private static bool IsSystem(INamespaceSymbol? ns)
        => ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
}
