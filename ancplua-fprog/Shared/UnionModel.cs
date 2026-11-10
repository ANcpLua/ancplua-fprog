using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>
/// The Fprog unions seen from one compilation (AGENTS.md, "Scope"). A union is a class or record
/// annotated <c>[Fprog.Union]</c>; its cases are the types whose direct base type is the union.
/// <para>
/// A union is identified by its definition (<see cref="GetUnion"/>): <c>Result&lt;int&gt;</c> and
/// <c>Result&lt;T&gt;</c> are the same union. A value has a constructed union type
/// (<see cref="GetUnionType"/>), and <see cref="GetCases"/> returns only the cases a value of that
/// type can be: with <c>IntOk : Result&lt;int&gt;</c>, a <c>Result&lt;string&gt;</c> is never an
/// <c>IntOk</c>. Cases are returned as definitions (<c>Result&lt;T&gt;.Ok</c>, never
/// <c>Result&lt;int&gt;.Ok</c>). Do not compare the type of a pattern with a case by hand:
/// <see cref="TryGetCoverage"/> does it and also checks the type arguments.
/// </para>
/// <para>
/// Create one instance per compilation (in <c>RegisterCompilationStartAction</c>, or in
/// <c>ReportSuppressions</c>) and share it between callbacks. It is thread-safe, finds cases lazily
/// by walking the namespaces of the compilation's source assembly and of the union's own assembly,
/// and gives the same answers whatever order callbacks arrive in. The static members need no instance.
/// </para>
/// </summary>
internal sealed partial class UnionModel(Compilation compilation)
{
    private readonly Compilation _compilation = compilation;

    // No Lazy<T>: it would cache an exception from the namespace walk forever. GetOrAdd stores
    // nothing when the walk throws, so the next call walks again. Threads that race for the first
    // walk of an assembly each build the same index, and one of them is kept.
    private readonly ConcurrentDictionary<IAssemblySymbol, Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>
        _casesByAssembly = new(SymbolEqualityComparer.Default);

    /// <summary>How <see cref="Matches"/> treats type parameters.</summary>
    private enum TypeMatching
    {
        /// <summary>
        /// Left is written in the type parameters of a case, right is the union type of a value: a type
        /// parameter on the left binds to the type on the right, one on the right matches anything.
        /// </summary>
        Bind,

        /// <summary>Both sides belong to a value: a type parameter on either side matches anything.</summary>
        Overlap,

        /// <summary>
        /// Left is written in the type parameters of a case, right is a type named in a pattern: a bound
        /// type parameter on the left needs the type it was bound to, an unbound one is not compared.
        /// </summary>
        Agree,

        /// <summary>No type parameter is special: both sides are the same type.</summary>
        Same
    }

    /// <summary>True when <paramref name="type"/> is a class or record annotated <c>[Fprog.Union]</c>.</summary>
    public static bool IsUnion(ITypeSymbol? type)
        => type is INamedTypeSymbol { TypeKind: TypeKind.Class } named
           && FprogAttributes.Contains(named.OriginalDefinition.GetAttributes(), FprogAttributes.Union);

    /// <summary>
    /// The constructed union type of a value whose static type is <paramref name="type"/>, otherwise
    /// <see langword="null"/>. A union type (<c>Result&lt;int&gt;</c>, nullable-annotated or not) is
    /// returned as it is. A type parameter is a union value when one of its constraint types is a
    /// union, directly or through another type parameter: with <c>where T : Result&lt;int&gt;</c> a
    /// <c>T</c> has the union type <c>Result&lt;int&gt;</c>. The first union found is returned.
    /// </summary>
    public static INamedTypeSymbol? GetUnionType(ITypeSymbol? type)
        => type switch
        {
            INamedTypeSymbol named when IsUnion(named) => named,
            ITypeParameterSymbol parameter => GetUnionOfConstraints(parameter, null),
            _ => null
        };

    /// <summary>
    /// The union definition (<c>Result&lt;T&gt;</c>) when a value whose static type is
    /// <paramref name="type"/> is a union value in the sense of <see cref="GetUnionType"/>,
    /// otherwise <see langword="null"/>.
    /// </summary>
    public static INamedTypeSymbol? GetUnion(ITypeSymbol? type) => GetUnionType(type)?.OriginalDefinition;

    /// <summary>
    /// The union definition when <paramref name="type"/> is a case, that is, its direct base type is
    /// a union; otherwise <see langword="null"/>. A type that derives from a case is not a case.
    /// </summary>
    public static INamedTypeSymbol? GetUnionOfCase(ITypeSymbol? type) => GetUnion(type?.BaseType);

    /// <summary>
    /// The case definitions that a value whose static type is <paramref name="type"/> can be, declared
    /// in the union's own assembly or in this compilation; empty when that is not a union value
    /// (<see cref="GetUnionType"/>). The union definition <c>Result&lt;T&gt;</c> and a
    /// <c>Result&lt;TValue&gt;</c> inside a generic method get every case, <c>Result&lt;string&gt;</c>
    /// gets neither <c>IntOk : Result&lt;int&gt;</c> nor <c>ListOk&lt;T&gt; : Result&lt;List&lt;T&gt;&gt;</c>.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> GetCases(ITypeSymbol? type)
    {
        if (GetUnionType(type) is not { } unionType)
            return [];

        return GetAllCases(unionType.OriginalDefinition).RemoveAll(definition => !TryBind(definition, unionType, out _));
    }

    private static INamedTypeSymbol? GetUnionOfConstraints(ITypeParameterSymbol parameter, HashSet<ITypeParameterSymbol>? visited)
    {
        foreach (var constraint in parameter.ConstraintTypes)
        {
            switch (constraint)
            {
                case INamedTypeSymbol named when IsUnion(named):
                    return named;
                case ITypeParameterSymbol other:
                    // Erroneous code can declare a cycle: where T : U where U : T.
                    visited ??= new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default) { parameter };
                    if (visited.Add(other) && GetUnionOfConstraints(other, visited) is { } union)
                        return union;

                    break;
            }
        }

        return null;
    }

    /// <summary>Every case definition of the union definition <paramref name="union"/>, whatever its type arguments.</summary>
    private ImmutableArray<INamedTypeSymbol> GetAllCases(INamedTypeSymbol union)
    {
        var sourceAssembly = _compilation.Assembly;
        var casesInSource = CasesIn(sourceAssembly, union);
        var home = union.ContainingAssembly;
        return home is null || SymbolEqualityComparer.Default.Equals(home, sourceAssembly)
            ? casesInSource
            : CasesIn(home, union).AddRange(casesInSource);
    }

    private ImmutableArray<INamedTypeSymbol> CasesIn(IAssemblySymbol assembly, INamedTypeSymbol union)
        => _casesByAssembly.GetOrAdd(assembly, IndexCases).TryGetValue(union, out var cases) ? cases : [];

    /// <summary>
    /// Maps every union to its cases declared in <paramref name="assembly"/>, in namespace-walk order
    /// (types of a namespace, each followed by its nested types, then child namespaces).
    /// </summary>
    private static Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> IndexCases(IAssemblySymbol assembly)
    {
        var index = new Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        Visit(assembly.GlobalNamespace);
        return index;

        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (var type in container.GetTypeMembers())
            {
                if (GetUnionOfCase(type) is { } union)
                    index[union] = index.TryGetValue(union, out var cases) ? cases.Add(type) : [type];

                Visit(type);
            }

            if (container is INamespaceSymbol namespaceSymbol)
            {
                foreach (var child in namespaceSymbol.GetNamespaceMembers())
                    Visit(child);
            }
        }
    }

    /// <summary>
    /// False when a value of the union type <paramref name="unionType"/> can never be the case
    /// <paramref name="caseDefinition"/>. Otherwise <paramref name="bindings"/> says what each type
    /// parameter of the case stands for in such a value; <see langword="null"/> when nothing was bound.
    /// </summary>
    private static bool TryBind(
        INamedTypeSymbol caseDefinition,
        INamedTypeSymbol unionType,
        out Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        bindings = null;
        return caseDefinition.BaseType is { } baseType && Matches(baseType, unionType, TypeMatching.Bind, ref bindings);
    }

    /// <summary>
    /// True when <paramref name="patternType"/>, a type named in a pattern, is <paramref name="caseSide"/>
    /// (the case, one of its base types or one of its interfaces) for a case bound by <see cref="TryBind"/>.
    /// </summary>
    private static bool Agrees(ITypeSymbol caseSide, ITypeSymbol patternType, Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
        => Matches(caseSide, patternType, TypeMatching.Agree, ref bindings);

    /// <summary>
    /// Compares two types by structure. Named types match when their definitions are equal and their
    /// type arguments match, including those carried by containing types (<c>Outer&lt;T&gt;.Result</c>);
    /// so tuple element names and nullable annotations do not matter. Arrays match by rank and element
    /// type. <c>object</c> and <c>dynamic</c> are the same type. Everything else must be equal.
    /// </summary>
    private static bool Matches(
        ITypeSymbol left,
        ITypeSymbol right,
        TypeMatching mode,
        ref Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        switch (mode)
        {
            case TypeMatching.Bind when left is ITypeParameterSymbol parameter:
                // Bound before (Same<T> : Result<Pair<T, T>>): both occurrences must be able to be one type.
                if (bindings is not null && bindings.TryGetValue(parameter, out var bound))
                    return Matches(bound, right, TypeMatching.Overlap, ref bindings);

                bindings ??= new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
                bindings.Add(parameter, right);
                return true;
            case TypeMatching.Agree when left is ITypeParameterSymbol parameter:
                return bindings is null
                       || !bindings.TryGetValue(parameter, out var expected)
                       || Matches(expected, right, TypeMatching.Same, ref bindings);
            case TypeMatching.Bind or TypeMatching.Overlap when left is ITypeParameterSymbol || right is ITypeParameterSymbol:
                return true;
        }

        switch (left)
        {
            case INamedTypeSymbol leftNamed when right is INamedTypeSymbol rightNamed:
                if (!SymbolEqualityComparer.Default.Equals(leftNamed.OriginalDefinition, rightNamed.OriginalDefinition))
                    return false;

                var leftArguments = leftNamed.TypeArguments;
                var rightArguments = rightNamed.TypeArguments;
                for (var index = 0; index < leftArguments.Length; index++)
                {
                    if (!Matches(leftArguments[index], rightArguments[index], mode, ref bindings))
                        return false;
                }

                return leftNamed.ContainingType is not { } leftOuter
                       || rightNamed.ContainingType is not { } rightOuter
                       || Matches(leftOuter, rightOuter, mode, ref bindings);
            case IArrayTypeSymbol leftArray when right is IArrayTypeSymbol rightArray:
                return leftArray.Rank == rightArray.Rank
                       && Matches(leftArray.ElementType, rightArray.ElementType, mode, ref bindings);
            default:
                return SymbolEqualityComparer.Default.Equals(left, right)
                       || (IsObjectOrDynamic(left) && IsObjectOrDynamic(right));
        }
    }

    private static bool IsObjectOrDynamic(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;
}
