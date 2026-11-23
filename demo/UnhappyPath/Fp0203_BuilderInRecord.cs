using System;
using System.Collections.Immutable;

namespace Demo.Fp0203_BuilderInRecord;

[Fprog.FunctionalCore]
public sealed record Draft(
    ImmutableArray<string>.Builder Lines, // expect: FP0203
    ImmutableDictionary<string, int>.Builder Counts) // expect: FP0203
{
    public int Size => Lines.Count + Counts.Count;
}

[Fprog.FunctionalCore]
public readonly struct Page
{
    public ImmutableList<string>.Builder Words { get; init; } // expect: FP0203
}

[Fprog.FunctionalCore]
public sealed record Frame(Memory<byte> Bytes, ArraySegment<byte> Header); // expect: FP0203, FP0203
