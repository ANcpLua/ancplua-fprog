using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0302_MethodGroup;

// Correct: a text that is no number is left out, and the caller sees how many were read.
[Fprog.FunctionalCore]
public static class Quantities
{
    public static ImmutableArray<int> All(ImmutableArray<string> texts)
        => texts.SelectMany(ReadOne).ToImmutableArray();

    public static Func<string, IEnumerable<int>> Parser() => ReadOne;

    public static string Name() => nameof(int.Parse);

    private static IEnumerable<int> ReadOne(string text)
        => int.TryParse(text, out var number) ? [number] : [];
}
