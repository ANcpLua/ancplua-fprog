using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>
/// Which cases of a union the arms of one <c>switch</c> expression name and handle; the result of
/// <see cref="UnionModel.TryGetCoverage"/>, which defines the terms. All cases are definitions
/// (<c>Result&lt;T&gt;.Ok</c>). The default value, which <see cref="UnionModel.TryGetCoverage"/>
/// returns together with <see langword="false"/>, has no cases and handles nothing.
/// <para>
/// FP0502 and FPS001 read different members on purpose, although AGENTS.md speaks of the same
/// definition. Reporting is lenient: FP0502 reports when <see cref="CasesNotNamed"/> is not empty.
/// Suppressing is strict: FPS001 suppresses CS8509 only when <see cref="HandlesEveryCase"/>.
/// A switch that names every case but can still reject a value (<c>Circle { Radius: > 0 }</c> alone)
/// gets neither: the compiler's CS8509 stays and says it.
/// </para>
/// </summary>
internal readonly struct UnionSwitchCoverage(
    INamedTypeSymbol union,
    ImmutableArray<INamedTypeSymbol> cases,
    ImmutableArray<INamedTypeSymbol> casesNotNamed,
    ImmutableArray<INamedTypeSymbol> casesWithoutOwnArm,
    ImmutableArray<INamedTypeSymbol> unhandledCases,
    bool armsCanNameEveryValue)
{
    /// <summary>
    /// The union definition (<c>Result&lt;T&gt;</c>) of the switched value. It is
    /// <see langword="null"/> in the default value only.
    /// </summary>
    public INamedTypeSymbol Union { get; } = union;

    /// <summary>
    /// The cases the switched value can be (<see cref="UnionModel.GetCases"/> of its union type) and
    /// that the code at the switch can name. A private nested case seen from outside, an internal case
    /// of another assembly and a case in a file-local type of another file are left out of this and of
    /// the other lists: no arm can name them. <see cref="HandlesEveryCase"/> is false when there is one.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> Cases => cases.IsDefault ? [] : cases;

    /// <summary>
    /// The <see cref="Cases"/> that no arm without a <c>when</c> clause names: no such arm has a type
    /// test for the case's own type, alone or as a side of <c>or</c> or <c>and</c>, whatever its
    /// subpatterns. A catch-all arm names nothing, <c>not Circle</c> names nothing, and a type test
    /// inside a subpattern names nothing. A subset of <see cref="CasesWithoutOwnArm"/>.
    /// <para>
    /// FP0502 reports when this is not empty. It does not report an arm that names a case but rejects
    /// some of its values (<c>Circle { Radius: > 0 }</c> alone, or <c>Ok(Inner.A)</c> next to
    /// <c>Ok(Inner.B)</c>): whether such arms leave a hole is the compiler's CS8509 to say, and FP0501
    /// reports catch-alls.
    /// </para>
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> CasesNotNamed => casesNotNamed.IsDefault ? [] : casesNotNamed;

    /// <summary>
    /// The <see cref="Cases"/> for which no single arm exists that has no <c>when</c> clause, names the
    /// case and matches every non-null value of it. These are the <see cref="CasesNotNamed"/> and the
    /// cases whose arms each reject some value; arms are never combined, so <c>Ok(Inner.A)</c> and
    /// <c>Ok(Inner.B)</c> together are not an own arm of <c>Ok</c>. A catch-all arm does not count.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> CasesWithoutOwnArm => casesWithoutOwnArm.IsDefault ? [] : casesWithoutOwnArm;

    /// <summary>
    /// The subset of <see cref="CasesWithoutOwnArm"/> that no catch-all arm without a <c>when</c> clause
    /// covers either: a value of such a case reaches no arm, or only arms that can reject it. A catch-all
    /// here is any arm that matches every non-null value of the case without naming it, also
    /// <c>not Circle</c> for <c>Square</c> and an interface that the case implements.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> UnhandledCases => unhandledCases.IsDefault ? [] : unhandledCases;

    /// <summary>
    /// True when all of this holds: there is at least one case in <see cref="Cases"/>; every one of them
    /// has its own arm (<see cref="CasesWithoutOwnArm"/> is empty); the code at the switch can name
    /// every case the value can be; and the union type of the value determines every type parameter of
    /// every case. FPS001 suppresses CS8509 only when this is true: a suppressor must not hide the
    /// warning on a switch that can still throw.
    /// <para>
    /// The last two conditions are stricter than the lists, which demand nothing the author cannot
    /// write. A case that the switch cannot name (see <see cref="Cases"/>) is in none of the lists, yet
    /// a value of it reaches no arm. And for <c>IntLit : Expr&lt;int&gt;</c> nested in
    /// <c>Expr&lt;T&gt;</c>, an <c>Expr&lt;string&gt;.IntLit</c> is an <c>Expr&lt;int&gt;</c> too, and no
    /// arm can name every such type; the lists do not compare a type parameter that the value does not
    /// determine. In both situations every list can be empty while this is false.
    /// </para>
    /// <para>
    /// What this does not see, on purpose. A reference type declared in code without nullable
    /// annotations and a type parameter without <c>?</c> count as not null in a subpattern:
    /// <c>Leaf(string name)</c> and <c>Result&lt;TValue&gt;.Ok(TValue value)</c> count as matching every
    /// value. The test host of this kit compiles without a nullable context, so its code is of that kind
    /// and <c>Shape.Named(string n)</c> must count as total there. A null in such a slot falls through at
    /// run time, and the suppressor does not see it. And an instance of a union that is not abstract is
    /// not a case, so this stays true for such a union; FP0401 reports it.
    /// </para>
    /// <para>
    /// Null is not a case either, so this says nothing about a null value. The compiler reports CS8509
    /// for a switch that names every case whether or not an arm matches null, so the author of FPS001
    /// must decide separately what to do when the switched value may be null and no arm handles null.
    /// In code with nullable annotations, the static type of the switched value is annotated <c>?</c>
    /// when the value may be null; observed with Roslyn 5.9.0, that is also so for a non-nullable value
    /// as soon as an arm tests for null (<c>null</c>, <c>not null</c>, <c>{ }</c>, <c>object</c>). In
    /// code without nullable annotations it has no annotation at all.
    /// </para>
    /// </summary>
    public bool HandlesEveryCase => armsCanNameEveryValue && !Cases.IsEmpty && CasesWithoutOwnArm.IsEmpty;
}
