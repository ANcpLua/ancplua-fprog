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
/// <c>Interlocked</c> and <c>Timer</c> in <c>System.Threading</c> (nested types included); at the member
/// name of a static member of such a type reached without a qualifier (<c>using static</c>); and at the
/// <c>lock</c> keyword. <c>using</c> directives and <c>var</c> are not reported.
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
        context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        context.RegisterSyntaxNodeAction(AnalyzeLock, SyntaxKind.LockStatement);
    }

    private static void AnalyzeLock(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var keyword = ((LockStatementSyntax)context.Node).LockKeyword;
        context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), keyword.ValueText));
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context)
    {
        var name = (SimpleNameSyntax)context.Node;
        if (name.IsVar
            || name.IsPartOfStructuredTrivia()
            || !CoreScope.IsInCore(context.ContainingSymbol)
            || name.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
        var type = symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor when IsAttributeName(name)
                => constructor.ContainingType,
            IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol when symbol.IsStatic && IsUnqualified(name)
                => symbol.ContainingType,
            _ => null
        };

        if (type is not null && IsForbidden(type))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.Identifier.GetLocation(), type.Name));
    }

    /// <summary>True when <paramref name="name"/> is written without a qualifier on its left.</summary>
    private static bool IsUnqualified(SimpleNameSyntax name)
        => name.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Name != name,
            QualifiedNameSyntax qualified => qualified.Right != name,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name != name,
            MemberBindingExpressionSyntax => false,
            _ => true
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
        var outermost = type;
        while (outermost.ContainingType is { } containing)
            outermost = containing;

        var ns = outermost.ContainingNamespace;
        if (IsWithinSystemChild(ns, "IO") || IsWithinSystemChild(ns, "Net") || IsWithinSystemChild(ns, "Data"))
            return true;

        return ns switch
        {
            _ when IsSystem(ns) => outermost.MetadataName == "Console",
            { Name: "Diagnostics" } when IsSystem(ns.ContainingNamespace) => outermost.MetadataName == "Process",
            { Name: "Threading" } when IsSystem(ns.ContainingNamespace)
                => outermost.MetadataName is "Thread" or "Monitor" or "Lock" or "Mutex" or "Semaphore"
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
