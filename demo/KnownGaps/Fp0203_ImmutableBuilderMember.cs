using System.Collections.Immutable;

namespace Demo.KnownGaps.Fp0203_ImmutableBuilderMember;

// Not reported today: the FP0203 row allows everything in System.Collections.Immutable, and a builder lives
// there although it is a mutable collection. A `with` copy shares the builder, and Add changes both records.
[Fprog.FunctionalCore]
public sealed record Draft(
    ImmutableArray<string>.Builder Lines,
    ImmutableDictionary<string, int>.Builder Counts)
{
    public int Size => Lines.Count + Counts.Count;
}

[Fprog.FunctionalCore]
public readonly struct Page
{
    public ImmutableList<string>.Builder Words { get; init; }
}
