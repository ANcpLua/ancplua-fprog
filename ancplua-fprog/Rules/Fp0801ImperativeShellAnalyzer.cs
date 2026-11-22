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
/// <c>Lock.Scope</c>, are not listed), and at the <c>lock</c> keyword. A static member of such a type that is
/// written without a type name token because a <c>using static</c> directive imports it (<c>WriteLine</c>,
/// also as a method group, in <c>nameof</c> or in a dynamic call) is reported at the member name token. A static
/// member inherited by the enclosing type is not: the base list already reports the type token.
/// <c>using</c> directives and <c>var</c> are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0801ImperativeShellAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0801",
        "Do not reach the imperative shell from the functional core",
        "'{0}' belongs to the imperative shell: take its result as a parameter (or return a value that describes the effect) and call it from a type outside [Fprog.FunctionalCore]");

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
        Report(context, keyword.GetLocation(), keyword.ValueText);
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

        var info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
        if (info.Symbol is { } symbol
            && ReferencedType(symbol, name) is { } type
            && IsForbidden(type))
        {
            Report(context, name.Identifier.GetLocation(), type.Name);
            return;
        }

        if (ImportedStaticMember(context, name, info) is { } member)
            Report(context, name.Identifier.GetLocation(), member.ContainingType.Name + "." + member.Name);
    }

    /// <summary>
    /// The forbidden static member that an unqualified <paramref name="name"/> reaches through a <c>using static</c>
    /// directive. For a method group (<c>nameof</c>, a dynamic call) every candidate must be such a member.
    /// </summary>
    private static ISymbol? ImportedStaticMember(
        SyntaxNodeAnalysisContext context, SimpleNameSyntax name, SymbolInfo info)
    {
        if (name.Parent is MemberAccessExpressionSyntax { Name: var accessed } && accessed == name
            || name.Parent is QualifiedNameSyntax { Right: var right } && right == name
            || name.Parent is AliasQualifiedNameSyntax { Name: var aliased } && aliased == name
            || name.Parent is MemberBindingExpressionSyntax)
        {
            return null;
        }

        if (info.Symbol is { } single)
            return IsImportedStatic(context, single) ? single : null;

        ISymbol? first = null;
        foreach (var candidate in info.CandidateSymbols)
        {
            if (!IsImportedStatic(context, candidate))
                return null;
            first ??= candidate;
        }

        return first;
    }

    private static bool IsImportedStatic(SyntaxNodeAnalysisContext context, ISymbol member)
    {
        if (member is not (IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol)
            || !member.IsStatic
            || member.ContainingType is not { } owner
            || !IsForbidden(owner))
        {
            return false;
        }

        // Reached through inheritance (or from inside the type itself): not an import.
        var target = owner.OriginalDefinition;
        for (var enclosing = context.ContainingSymbol as INamedTypeSymbol ?? context.ContainingSymbol?.ContainingType;
             enclosing is not null;
             enclosing = enclosing.ContainingType)
        {
            for (var type = enclosing; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, target))
                    return false;
            }
        }

        return true;
    }

    private static void Report(SyntaxNodeAnalysisContext context, Location location, string subject)
    {
        if (!IsRepeatedUnionPass(context))
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, subject));
    }

    /// <summary>
    /// True for the extra passes that Roslyn 5.9.0 makes over a node in a C# 15 union declaration: after the genuine
    /// pass, the node is analyzed again with the union and with the union's container as containing symbol (a node
    /// in the union's header only with the container). A genuine pass carries a type only where the declaration
    /// that holds the node declares that type: its base list, constraints and attributes (also on its type
    /// parameters), a delegate signature, an extension block parameter, or a union's case list. Everywhere else
    /// the genuine pass carries a member. A pass with a namespace as containing symbol is never in core.
    /// </summary>
    private static bool IsRepeatedUnionPass(SyntaxNodeAnalysisContext context)
        => context.ContainingSymbol is INamedTypeSymbol type
           && context.Node.FirstAncestorOrSelf<MemberDeclarationSyntax>() is { } declaration
           && !SymbolEqualityComparer.Default.Equals(
               context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken), type);

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
