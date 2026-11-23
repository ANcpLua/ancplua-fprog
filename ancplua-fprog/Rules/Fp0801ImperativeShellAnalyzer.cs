using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0801: the functional core references the imperative shell. Reported at every written name token
///     of a type in <c>System.IO</c>, <c>System.Net</c> or <c>System.Data</c> (and the namespaces below them),
///     of <c>System.Console</c>, <c>System.Diagnostics.Process</c>, or one of the threading types
///     <c>Thread</c>, <c>Monitor</c>, <c>Lock</c>, <c>Mutex</c>, <c>Semaphore</c>, <c>SemaphoreSlim</c>,
///     <c>Interlocked</c> and <c>Timer</c> in <c>System.Threading</c> (their nested types, such as
///     <c>Lock.Scope</c>, are not listed), and at the <c>lock</c> keyword. <c>using</c> directives and <c>var</c>
///     are not reported.
///     <para>
///         Code that uses the shell without writing such a type name is reported too, at a name token
///         (<see cref="Fp0801ShellUses" />):
///     </para>
///     <list type="bullet">
///         <item>
///             A static member of a forbidden type (method, also as a method group, property, field, constant, enum
///             member, event) written as a simple name that binds through <c>using static</c> or
///             <c>global using static</c>:
///             reported at that name, whatever else the member contains. This includes pure members such as
///             <c>GetExtension</c> from <c>System.IO.Path</c> or the constant <c>Open</c> from <c>System.IO.FileMode</c>;
///             that is deliberate, because the same code with the type written (<c>Path.GetExtension</c>,
///             <c>FileMode.Open</c>) is reported at the type name. Not reported: the operand of <c>nameof</c>; a static
///             member that an enclosing type inherits from a forbidden base type and uses unqualified (the base list names
///             the
///             type and is reported); a call bound at run time unless every candidate is a member of a forbidden type.
///         </item>
///         <item>
///             An instance member (method, property, field, event) on an uncovered receiver whose static type is
///             forbidden or whose bound member is declared on a forbidden type: reported at the member's name, for example
///             <c>ReadLine</c> in <c>var reader = Shell.Open(path); reader.ReadLine()</c> or <c>ReadToEnd</c> in
///             <c>Shell.Open(path).ReadToEnd()</c> when <c>Shell</c> is not core. Only the first such member of a chain is
///             reported. Coverage is decided where the shell type enters the chain, walking from the receiver to the left.
///             The receiver is covered, and nothing is reported, when FP0801 reports a token in it, or when the shell type
///             enters at <c>this</c> or <c>base</c> or at a declaration in core with a written type (a parameter,
///             primary-constructor or extension-block parameter, local, field, property or method, whether or not that
///             type
///             is forbidden: <c>Size(ShellBuffer buffer) => buffer.Length</c> is not reported), or at a <c>var</c>,
///             <c>foreach</c>, pattern, <c>out var</c> or lambda variable whose source is covered. It is uncovered when
///             the
///             shell type enters at a member declared outside core whose declared type names it, even when the chain
///             starts
///             at a typed core parameter: <c>Open</c> in <c>repo.Connection.Open()</c> is reported. That also reports pure
///             members of shell types held by a non-core type, such as <c>Host</c> in <c>contact.Address.Host</c>
///             (<c>MailAddress</c>) or <c>ToString</c> in <c>reply.Status.ToString()</c> (<c>HttpStatusCode</c>); that is
///             deliberate, because the same code with the type written is reported at the type name. A member on a value
///             whose static type is not forbidden and that is not declared on a forbidden type is never reported. When the
///             analysis cannot tell, the receiver counts as covered.
///         </item>
///         <item>
///             A target-typed <c>new(...)</c> that creates a forbidden type as an argument for a parameter of a member
///             outside core: reported at the <c>new</c> keyword.
///         </item>
///     </list>
///     <para>
///         Deliberately not reported (the type is written nowhere, but no name token belongs to it, or the analysis cannot
///         tell): indexers, operators, conversions, <c>foreach</c> enumeration and <c>using</c> disposal of a shell
///         object;
///         calls through a delegate; members on <c>dynamic</c>; extension members; a shell object passed straight to
///         another member; a receiver that is a conditional, a cast, a tuple, an anonymous object or a query range
///         variable; and a value whose shell type comes from a static generic method (<c>Enumerable.First(xs)</c>).
///     </para>
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
        context.RegisterSyntaxNodeAction(AnalyzeTargetTypedCreation, SyntaxKind.ImplicitObjectCreationExpression);
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
            return;

        var info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
        if (info.Symbol is { } symbol && ReferencedType(symbol, name) is { } type && IsForbidden(type))
        {
            Report(context, name.Identifier.GetLocation(), type.Name);
            return;
        }

        var uses = new Fp0801ShellUses(context.SemanticModel, context.ContainingSymbol, context.CancellationToken);
        if ((uses.ImportedStaticUse(name, info) ?? uses.UncoveredInstanceUse(name, info)) is { } shell)
            Report(context, name.Identifier.GetLocation(), shell.Name);
    }

    private static void AnalyzeTargetTypedCreation(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var creation = (ImplicitObjectCreationExpressionSyntax)context.Node;
        var uses = new Fp0801ShellUses(context.SemanticModel, context.ContainingSymbol, context.CancellationToken);
        if (uses.UncoveredTargetTypedCreation(creation) is { } type)
            Report(context, creation.NewKeyword.GetLocation(), type.Name);
    }

    private static void Report(SyntaxNodeAnalysisContext context, Location location, string subject)
    {
        if (!IsRepeatedUnionPass(context))
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, subject));
    }

    /// <summary>
    ///     True for the extra passes that Roslyn 5.9.0 makes over a node in a C# 15 union declaration: after the genuine
    ///     pass, the node is analyzed again with the union and with the union's container as containing symbol (a node
    ///     in the union's header only with the container). A genuine pass carries a type only where the declaration
    ///     that holds the node declares that type: its base list, constraints and attributes (also on its type
    ///     parameters), a delegate signature, an extension block parameter, or a union's case list. Everywhere else
    ///     the genuine pass carries a member. A pass with a namespace as containing symbol is never in core.
    /// </summary>
    private static bool IsRepeatedUnionPass(SyntaxNodeAnalysisContext context)
    {
        return context.ContainingSymbol is INamedTypeSymbol type
               && context.Node.FirstAncestorOrSelf<MemberDeclarationSyntax>() is { } declaration
               && !SymbolEqualityComparer.Default.Equals(
                   context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken), type);
    }

    /// <summary>The type that <paramref name="name" /> names (for an attribute name, the attribute type).</summary>
    internal static INamedTypeSymbol? ReferencedType(ISymbol symbol, SimpleNameSyntax name)
    {
        return symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor when IsAttributeName(name)
                => constructor.ContainingType,
            _ => null
        };
    }

    /// <summary>True when <paramref name="name" /> is the type name of an attribute (possibly qualified).</summary>
    private static bool IsAttributeName(SimpleNameSyntax name)
    {
        SyntaxNode current = name;
        while ((current.Parent is QualifiedNameSyntax qualified && qualified.Right == current)
               || (current.Parent is AliasQualifiedNameSyntax aliasQualified && aliasQualified.Name == current))
            current = current.Parent;

        return current.Parent is AttributeSyntax attribute && attribute.Name == current;
    }

    internal static bool IsForbidden(INamedTypeSymbol type)
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

    /// <summary>True when <paramref name="ns" /> is <c>System.</c><paramref name="segment" /> or a namespace below it.</summary>
    private static bool IsWithinSystemChild(INamespaceSymbol? ns, string segment)
    {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            if (current.Name == segment && IsSystem(current.ContainingNamespace))
                return true;

        return false;
    }

    private static bool IsSystem(INamespaceSymbol? ns)
    {
        return ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
    }
}