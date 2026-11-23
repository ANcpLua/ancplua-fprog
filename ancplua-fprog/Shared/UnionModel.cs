using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>
///     The Fprog unions seen from one compilation (AGENTS.md, "Scope"). A union is a class or record
///     annotated <c>[Fprog.Union]</c>; its cases are the types whose direct base type is the union.
///     <para>
///         A union is identified by its definition (<see cref="GetUnion" />): <c>Result&lt;int&gt;</c> and
///         <c>Result&lt;T&gt;</c> are the same union. A value has a constructed union type
///         (<see cref="GetUnionType" />), and <see cref="GetCases" /> returns only the cases a value of that
///         type can be: with <c>IntOk : Result&lt;int&gt;</c>, a <c>Result&lt;string&gt;</c> is never an
///         <c>IntOk</c>. Cases are returned as definitions (<c>Result&lt;T&gt;.Ok</c>, never
///         <c>Result&lt;int&gt;.Ok</c>). Do not compare the type of a pattern with a case by hand:
///         <see cref="TryGetCoverage" /> does it and also checks the type arguments.
///     </para>
///     <para>
///         Create one instance per compilation (in <c>RegisterCompilationStartAction</c>, or in
///         <c>ReportSuppressions</c>) and share it between callbacks. It is thread-safe, finds cases lazily
///         by walking the namespaces of the assemblies that can declare one (<see cref="GetCases" />), and
///         gives the same answers whatever order callbacks arrive in. The static members need no instance.
///     </para>
/// </summary>
internal sealed partial class UnionModel(Compilation compilation)
{
    // For a union whose assembly is not the source assembly: the assemblies that can declare a case of it.
    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<IAssemblySymbol>>
        _caseAssembliesByUnion = new(SymbolEqualityComparer.Default);

    // No Lazy<T>: it would cache an exception from the namespace walk forever. GetOrAdd stores
    // nothing when the walk throws, so the next call walks again. Threads that race for the first
    // walk of an assembly each build the same index, and one of them is kept.
    private readonly ConcurrentDictionary<IAssemblySymbol,
            Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>
        _casesByAssembly = new(SymbolEqualityComparer.Default);

    private readonly Compilation _compilation = compilation;

    /// <summary>True when <paramref name="type" /> is a class or record annotated <c>[Fprog.Union]</c>.</summary>
    public static bool IsUnion(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol { TypeKind: TypeKind.Class } named
               && FprogAttributes.Contains(named.OriginalDefinition.GetAttributes(), FprogAttributes.Union);
    }

    /// <summary>
    ///     The constructed union type of a value whose static type is <paramref name="type" />, otherwise
    ///     <see langword="null" />. A union type (<c>Result&lt;int&gt;</c>, nullable-annotated or not) is
    ///     returned as it is. A type parameter is a union value when one of its constraint types is a
    ///     union, directly or through another type parameter: with <c>where T : Result&lt;int&gt;</c> a
    ///     <c>T</c> has the union type <c>Result&lt;int&gt;</c>. The first union found is returned.
    /// </summary>
    public static INamedTypeSymbol? GetUnionType(ITypeSymbol? type)
    {
        return type switch
        {
            INamedTypeSymbol named when IsUnion(named) => named,
            ITypeParameterSymbol parameter => GetUnionOfConstraints(parameter, null),
            _ => null
        };
    }

    /// <summary>
    ///     The union definition (<c>Result&lt;T&gt;</c>) when a value whose static type is
    ///     <paramref name="type" /> is a union value in the sense of <see cref="GetUnionType" />,
    ///     otherwise <see langword="null" />.
    /// </summary>
    public static INamedTypeSymbol? GetUnion(ITypeSymbol? type)
    {
        return GetUnionType(type)?.OriginalDefinition;
    }

    /// <summary>
    ///     The union definition when <paramref name="type" /> is a case, that is, its direct base type is
    ///     a union; otherwise <see langword="null" />. A type that derives from a case is not a case.
    /// </summary>
    public static INamedTypeSymbol? GetUnionOfCase(ITypeSymbol? type)
    {
        return GetUnion(type?.BaseType);
    }

    /// <summary>
    ///     The case definitions that a value whose static type is <paramref name="type" /> can be; empty
    ///     when that is not a union value (<see cref="GetUnionType" />). Cases are found in the union's own
    ///     assembly, then in every referenced assembly that references that assembly or an assembly that
    ///     forwards the union to it, then in this compilation. The union definition <c>Result&lt;T&gt;</c> and a
    ///     <c>Result&lt;TValue&gt;</c> inside a generic method get every case, <c>Result&lt;string&gt;</c>
    ///     gets neither <c>IntOk : Result&lt;int&gt;</c> nor <c>ListOk&lt;T&gt; : Result&lt;List&lt;T&gt;&gt;</c>.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> GetCases(ITypeSymbol? type)
    {
        if (GetUnionType(type) is not { } unionType)
            return [];

        return GetAllCases(unionType.OriginalDefinition)
            .RemoveAll(definition => !TryBind(definition, unionType, out _));
    }

    private static INamedTypeSymbol? GetUnionOfConstraints(ITypeParameterSymbol parameter,
        HashSet<ITypeParameterSymbol>? visited)
    {
        foreach (var constraint in parameter.ConstraintTypes)
            switch (constraint)
            {
                case INamedTypeSymbol named when IsUnion(named):
                    return named;
                case ITypeParameterSymbol other:
                    // Roslyn 5.9.0 breaks a constraint cycle (where T : U where U : T) itself by dropping one
                    // of the constraints. The set only guards against a symbol for which it does not.
                    visited ??= new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default) { parameter };
                    if (visited.Add(other) && GetUnionOfConstraints(other, visited) is { } union)
                        return union;

                    break;
            }

        return null;
    }

    /// <summary>Every case definition of the union definition <paramref name="union" />, whatever its type arguments.</summary>
    private ImmutableArray<INamedTypeSymbol> GetAllCases(INamedTypeSymbol union)
    {
        var sourceAssembly = _compilation.Assembly;
        var home = union.ContainingAssembly;
        if (home is null || SymbolEqualityComparer.Default.Equals(home, sourceAssembly))
            return CasesIn(sourceAssembly, union);

        var cases = ImmutableArray<INamedTypeSymbol>.Empty;
        foreach (var assembly in GetCaseAssemblies(union, home))
            cases = cases.AddRange(CasesIn(assembly, union));

        return cases;
    }

    /// <summary>
    ///     The assemblies that can declare a case of <paramref name="union" /> from <paramref name="home" />,
    ///     which is not the source assembly: <paramref name="home" /> itself, the referenced assemblies that
    ///     reference it or an assembly that forwards the union to it (in the order of the references of the
    ///     compilation), and the source assembly.
    /// </summary>
    private ImmutableArray<IAssemblySymbol> GetCaseAssemblies(INamedTypeSymbol union, IAssemblySymbol home)
    {
        if (_caseAssembliesByUnion.TryGetValue(union, out var known))
            return known;

        // Only a top-level type can be forwarded; a nested union moves with it.
        var topLevel = union;
        while (topLevel.ContainingType is { } outer)
            topLevel = outer;

        var metadataName = GetFullMetadataName(topLevel);
        var assemblies = ImmutableArray.CreateBuilder<IAssemblySymbol>();
        assemblies.Add(home);
        foreach (var referenced in _compilation.SourceModule.ReferencedAssemblySymbols)
            if (!SymbolEqualityComparer.Default.Equals(referenced, home) &&
                References(referenced, home, topLevel, metadataName))
                assemblies.Add(referenced);

        assemblies.Add(_compilation.Assembly);
        return _caseAssembliesByUnion.GetOrAdd(union, assemblies.ToImmutable());
    }

    /// <summary>
    ///     True when <paramref name="assembly" /> references <paramref name="home" />, or references an assembly
    ///     that forwards <paramref name="topLevel" /> (named <paramref name="metadataName" />) to where it is now:
    ///     a case compiled against the former assembly of a union reaches the union that way.
    /// </summary>
    private static bool References(IAssemblySymbol assembly, IAssemblySymbol home, INamedTypeSymbol topLevel,
        string metadataName)
    {
        foreach (var module in assembly.Modules)
        foreach (var candidate in module.ReferencedAssemblySymbols)
            if (SymbolEqualityComparer.Default.Equals(candidate, home)
                || SymbolEqualityComparer.Default.Equals(candidate.ResolveForwardedType(metadataName), topLevel))
                return true;

        return false;
    }

    /// <summary>The name that <see cref="IAssemblySymbol.ResolveForwardedType" /> takes: <c>Lib.Result`1</c>.</summary>
    private static string GetFullMetadataName(INamedTypeSymbol topLevel)
    {
        var name = topLevel.MetadataName;
        for (var space = topLevel.ContainingNamespace;
             space is { IsGlobalNamespace: false };
             space = space.ContainingNamespace)
            name = space.MetadataName + "." + name;

        return name;
    }

    private ImmutableArray<INamedTypeSymbol> CasesIn(IAssemblySymbol assembly, INamedTypeSymbol union)
    {
        return _casesByAssembly.GetOrAdd(assembly, IndexCases).TryGetValue(union, out var cases) ? cases : [];
    }

    /// <summary>
    ///     Maps every union to its cases declared in <paramref name="assembly" />, in namespace-walk order
    ///     (types of a namespace, each followed by its nested types, then child namespaces).
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
                foreach (var child in namespaceSymbol.GetNamespaceMembers())
                    Visit(child);
        }
    }

    /// <summary>
    ///     False when a value of the union type <paramref name="unionType" /> can never be the case
    ///     <paramref name="caseDefinition" />. Otherwise <paramref name="bindings" /> says what each type
    ///     parameter of the case stands for in such a value; <see langword="null" /> when nothing was bound.
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
    ///     True when <paramref name="patternType" />, a type named in a pattern, is <paramref name="caseSide" />
    ///     (the case, one of its base types or one of its interfaces) for a case bound by <see cref="TryBind" />.
    /// </summary>
    private static bool Agrees(ITypeSymbol caseSide, ITypeSymbol patternType,
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        return Matches(caseSide, patternType, TypeMatching.Agree, ref bindings);
    }

    /// <summary>
    ///     True when the union type of the value determines every type parameter of the case and of the
    ///     types that contain it. It does not for <c>IntLit : Expr&lt;int&gt;</c> nested in <c>Expr&lt;T&gt;</c>
    ///     (an <c>Expr&lt;string&gt;.IntLit</c> is an <c>Expr&lt;int&gt;</c> too) or
    ///     <c>Tagged&lt;TTag&gt; : Result&lt;int&gt;</c>.
    /// </summary>
    private static bool BindsEveryTypeParameter(INamedTypeSymbol caseDefinition,
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        var typeParameters = 0;
        for (var type = caseDefinition; type is not null; type = type.ContainingType)
            typeParameters += type.TypeParameters.Length;

        return typeParameters == (bindings?.Count ?? 0);
    }

    /// <summary>
    ///     The case as a value sees it: <paramref name="definition" /> with every bound type parameter
    ///     replaced by what <see cref="TryBind" /> bound it to (<c>Result&lt;string?&gt;.Ok</c> for
    ///     <c>Result&lt;T&gt;.Ok</c> and a <c>Result&lt;string?&gt;</c>). An unbound type parameter stays.
    /// </summary>
    private static INamedTypeSymbol ConstructForValue(INamedTypeSymbol definition,
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        if (bindings is null)
            return definition;

        var type = definition;
        if (definition.ContainingType is { } outer)
            // The nested type as a member of its constructed containing type.
            foreach (var member in ConstructForValue(outer, bindings).GetTypeMembers(definition.Name, definition.Arity))
                if (SymbolEqualityComparer.Default.Equals(member.OriginalDefinition, definition))
                    type = member;

        var parameters = definition.TypeParameters;
        if (parameters.IsEmpty || !SymbolEqualityComparer.Default.Equals(type, type.ConstructedFrom))
            return type;

        var arguments = new ITypeSymbol[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
            arguments[index] = bindings.TryGetValue(parameters[index], out var bound)
                ? bound
                : type.TypeParameters[index];

        return type.Construct(arguments);
    }

    /// <summary>
    ///     The construction of <paramref name="definition" /> that <paramref name="type" /> is, derives from
    ///     or implements; <see langword="null" /> when there is none. A type can implement two constructions
    ///     of one interface (<c>IHas&lt;object&gt;</c> and <c>IHas&lt;string?&gt;</c>): then the first is
    ///     returned, or with <paramref name="sameAs" /> only the one that is <paramref name="sameAs" /> but for
    ///     nullable annotations.
    /// </summary>
    private static INamedTypeSymbol? FindConstruction(ITypeSymbol type, ITypeSymbol definition,
        INamedTypeSymbol? sameAs = null)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
            if (IsConstruction(current, definition, sameAs))
                return current;

        foreach (var implemented in type.AllInterfaces)
            if (IsConstruction(implemented, definition, sameAs))
                return implemented;

        return null;
    }

    private static bool IsConstruction(INamedTypeSymbol candidate, ITypeSymbol definition, INamedTypeSymbol? sameAs)
    {
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? unused = null;
        return SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, definition)
               && (sameAs is null || Matches(candidate, sameAs, TypeMatching.Same, ref unused));
    }

    /// <summary>
    ///     True when <paramref name="patternType" />, the same type as <paramref name="valueType" /> otherwise,
    ///     lacks a <c>?</c> that <paramref name="valueType" /> has somewhere.
    /// </summary>
    private static bool LacksAnnotation(ITypeSymbol patternType, ITypeSymbol valueType)
    {
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? unused = null;
        return !Matches(valueType, patternType, TypeMatching.SameKeepingAnnotations, ref unused);
    }

    /// <summary>
    ///     True when <paramref name="type" /> is a type parameter or is built from one (<c>List&lt;T&gt;</c>, <c>T[]</c>
    ///     ).
    /// </summary>
    private static bool MentionsTypeParameter(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol:
                return true;
            case IArrayTypeSymbol array:
                return MentionsTypeParameter(array.ElementType);
            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments)
                    if (MentionsTypeParameter(argument))
                        return true;

                return named.ContainingType is { } outer && MentionsTypeParameter(outer);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Compares two types by structure. Named types match when their definitions are equal and their
    ///     type arguments match, including those carried by containing types (<c>Outer&lt;T&gt;.Result</c>);
    ///     so tuple element names do not matter, and nullable annotations only for
    ///     <see cref="TypeMatching.SameKeepingAnnotations" />. Arrays match by rank and element type.
    ///     <c>object</c> and <c>dynamic</c> are the same type. Everything else must be equal.
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
            case TypeMatching.Bind or TypeMatching.Overlap
                when left is ITypeParameterSymbol || right is ITypeParameterSymbol:
                return true;
            case TypeMatching.SameKeepingAnnotations
                when left.NullableAnnotation == NullableAnnotation.Annotated &&
                     right.NullableAnnotation != NullableAnnotation.Annotated:
                return false;
        }

        switch (left)
        {
            case INamedTypeSymbol leftNamed when right is INamedTypeSymbol rightNamed:
                if (!SymbolEqualityComparer.Default.Equals(leftNamed.OriginalDefinition, rightNamed.OriginalDefinition))
                    return false;

                var leftArguments = leftNamed.TypeArguments;
                var rightArguments = rightNamed.TypeArguments;
                for (var index = 0; index < leftArguments.Length; index++)
                    if (!Matches(leftArguments[index], rightArguments[index], mode, ref bindings))
                        return false;

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
    {
        return type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;
    }

    /// <summary>How <see cref="Matches" /> treats type parameters.</summary>
    private enum TypeMatching
    {
        /// <summary>
        ///     Left is written in the type parameters of a case, right is the union type of a value: a type
        ///     parameter on the left binds to the type on the right, one on the right matches anything.
        /// </summary>
        Bind,

        /// <summary>Both sides belong to a value: a type parameter on either side matches anything.</summary>
        Overlap,

        /// <summary>
        ///     Left is written in the type parameters of a case, right is a type named in a pattern: a bound
        ///     type parameter on the left needs the type it was bound to, an unbound one is not compared.
        /// </summary>
        Agree,

        /// <summary>No type parameter is special: both sides are the same type.</summary>
        Same,

        /// <summary>
        ///     Like <see cref="Same" />, and the right side nowhere lacks a <c>?</c> that the left side has:
        ///     <c>Result&lt;string&gt;</c> on the right lacks one of <c>Result&lt;string?&gt;</c>.
        /// </summary>
        SameKeepingAnnotations
    }
}