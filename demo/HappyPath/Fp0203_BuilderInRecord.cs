using System.Collections.Immutable;

namespace Demo.Fp0203_BuilderInRecord;

[Fprog.FunctionalCore]
public sealed record Draft(ImmutableArray<string> Lines, ImmutableDictionary<string, int> Counts)
{
    public int Size => Lines.Length + Counts.Count;
}

[Fprog.FunctionalCore]
public readonly struct Page
{
    public ImmutableList<string> Words { get; init; }
}

[Fprog.FunctionalCore]
public sealed record Frame(ImmutableArray<byte> Bytes, ImmutableArray<byte> Header);
