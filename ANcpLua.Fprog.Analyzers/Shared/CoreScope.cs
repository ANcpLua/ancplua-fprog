using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>Decides whether a symbol is in the functional core (AGENTS.md, "Scope").</summary>
internal static class CoreScope
{
    /// <summary>
    ///     True when <paramref name="symbol" /> is a type in core, or a member, local function, lambda,
    ///     parameter or type parameter whose containing type is in core. A type is in core when it or a
    ///     type that contains it (any depth) is annotated <c>[Fprog.FunctionalCore]</c>, or when its
    ///     assembly is annotated <c>[assembly: Fprog.FunctionalCore]</c>. Symbols outside any type
    ///     (namespaces, assemblies, array types) are not in core.
    /// </summary>
    public static bool IsInCore(ISymbol? symbol)
    {
        var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
        if (type is null)
            return false;

        return HasMarkedType(type) || type.ContainingAssembly is { } assembly && HasMarkedAssembly(assembly);
    }

    private static bool HasMarkedType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (FprogAttributes.Contains(current.GetAttributes(), FprogAttributes.FunctionalCore))
                return true;

        return false;
    }

    private static bool HasMarkedAssembly(IAssemblySymbol assembly)
    {
        return FprogAttributes.Contains(assembly.GetAttributes(), FprogAttributes.FunctionalCore);
    }

    internal sealed class Model
    {
        private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _types = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<IAssemblySymbol, bool> _assemblies = new(SymbolEqualityComparer.Default);

        public bool Contains(ISymbol? symbol)
        {
            var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
            if (type is null)
                return false;

            if (type.ContainingAssembly is { } assembly && ContainsAssembly(assembly))
                return true;

            return _types.GetOrAdd(type.OriginalDefinition, static definition => HasMarkedType(definition));
        }

        private bool ContainsAssembly(IAssemblySymbol assembly)
        {
            return _assemblies.GetOrAdd(assembly, static candidate => HasMarkedAssembly(candidate));
        }
    }

    /// <summary>
    ///     True when <paramref name="symbol" /> is written in source; false when the compiler synthesized it
    ///     or it was read from a compiled assembly (AGENTS.md, "Scope": only members declared in source count).
    ///     <para>
    ///         "In source" includes the source of a referenced project that this compilation sees as a
    ///         compilation and not as a DLL (in an IDE, or in a test with several projects). To ask for this
    ///         compilation only, also compare <c>symbol.ContainingAssembly</c> with <c>compilation.Assembly</c>.
    ///     </para>
    ///     <para>
    ///         With Roslyn 5.12.0, <c>record Circle(double Radius)</c> has two declared members: the positional
    ///         property <c>Radius</c> (its declaring syntax is the parameter) and the primary constructor.
    ///         The primary constructor's parameter is also declared. Synthesized members include
    ///         <c>Deconstruct</c> and its <c>out</c> parameter, the copy
    ///         constructor, <c>EqualityContract</c>, <c>ToString</c>, <c>PrintMembers</c>, both <c>Equals</c>,
    ///         <c>GetHashCode</c>, <c>==</c> and <c>!=</c>, <c>&lt;Clone&gt;$</c>, the backing field of
    ///         <c>Radius</c> and the accessors of <c>Radius</c> (no <c>get</c> or <c>init</c> keyword exists).
    ///         A member the record declares itself (its own <c>Deconstruct</c> or <c>ToString</c>) is declared.
    ///         An implicit default constructor and the backing field of any auto-property are not declared.
    ///     </para>
    ///     <para>
    ///         <see cref="ISymbol.IsImplicitlyDeclared" /> alone is not enough. It is false for the parameters of
    ///         synthesized members (the <c>out</c> parameters of a record's <c>Deconstruct</c>), which have no
    ///         declaring syntax; for the class <c>Program</c> and the entry point <c>&lt;Main&gt;$</c> that hold
    ///         top-level statements, whose only declaring syntax is the whole file (a <c>partial class Program</c>
    ///         that is written out is declared); and for every symbol read from a compiled assembly.
    ///     </para>
    ///     <para>
    ///         Not declared either: the <c>value</c> parameter of a setter, and the copy of an indexer parameter
    ///         that the code of an accessor refers to. So do not ask this about a parameter that a body uses.
    ///     </para>
    /// </summary>
    public static bool IsDeclaredInSource(ISymbol symbol)
    {
        if (symbol.IsImplicitlyDeclared)
            return false;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
            // Top-level statements: the whole file is the only syntax of their class and entry point.
            if (reference.GetSyntax() is not ICompilationUnitSyntax)
                return true;

        return false;
    }
}
