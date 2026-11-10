using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>
/// Which cases of a union the arms of one <c>switch</c> expression handle; the result of
/// <see cref="UnionModel.TryGetCoverage"/>, which defines "handles a case". All cases are definitions
/// (<c>Result&lt;T&gt;.Ok</c>). The default value has no cases and handles nothing.
/// </summary>
internal readonly struct UnionSwitchCoverage(
    INamedTypeSymbol union,
    ImmutableArray<INamedTypeSymbol> cases,
    ImmutableArray<INamedTypeSymbol> casesWithoutOwnArm,
    ImmutableArray<INamedTypeSymbol> unhandledCases)
{
    /// <summary>The union definition (<c>Result&lt;T&gt;</c>) of the switched value.</summary>
    public INamedTypeSymbol Union { get; } = union;

    /// <summary>
    /// The cases the switched value can be (<see cref="UnionModel.GetCases"/> of its union type) and
    /// that the code at the switch can name. A private nested case seen from outside, an internal case
    /// of another assembly and a file-local case of another file are left out: no arm can name them.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> Cases => cases.IsDefault ? [] : cases;

    /// <summary>
    /// The <see cref="Cases"/> for which no arm exists that has no <c>when</c> clause, tests for the
    /// case's own type and matches every non-null value of the case.
    /// <para>
    /// FP0502 reports when this is not empty. That is the stricter reading: a catch-all arm
    /// (<c>_</c>, <c>var x</c>, <c>not null</c>, <c>{ }</c>, the union type, a base type or an interface)
    /// does not count as handling a case; FP0501 reports the catch-all itself.
    /// </para>
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> CasesWithoutOwnArm => casesWithoutOwnArm.IsDefault ? [] : casesWithoutOwnArm;

    /// <summary>
    /// The subset of <see cref="CasesWithoutOwnArm"/> that no catch-all arm without a <c>when</c> clause
    /// covers either: a value of such a case reaches no arm, or only arms that can reject it.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> UnhandledCases => unhandledCases.IsDefault ? [] : unhandledCases;

    /// <summary>
    /// True when the union has at least one case in <see cref="Cases"/> and every one of them has its
    /// own arm. FPS001 suppresses CS8509 only when this is true.
    /// <para>
    /// Null is not a case, so this says nothing about a null value. The compiler reports CS8509 for a
    /// switch that names every case whether or not an arm matches null, so the author of FPS001 must
    /// decide separately what to do when the switched value may be null and no arm handles null. The
    /// static type of the switched value is annotated <c>?</c> when the value may be null; observed with
    /// Roslyn 5.9.0, that is also so for a non-nullable value as soon as an arm tests for null
    /// (<c>null</c>, <c>not null</c>, <c>{ }</c>, <c>object</c>).
    /// </para>
    /// </summary>
    public bool HandlesEveryCase => !Cases.IsEmpty && CasesWithoutOwnArm.IsEmpty;
}
