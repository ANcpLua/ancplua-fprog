using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0302_MethodGroup;

// Bug: int.Parse runs once per element and throws FormatException for the first bad text, although no call
// `int.Parse(...)` is written anywhere.
[Fprog.FunctionalCore]
public static class Quantities
{
    public static ImmutableArray<int> All(ImmutableArray<string> texts)
        => texts.Select(int.Parse).ToImmutableArray(); // expect: FP0302

    public static Func<string, int> Parser() => int.Parse; // expect: FP0302

    public static Func<IEnumerable<int>, int> Head() => Enumerable.First; // expect: FP0302
}
