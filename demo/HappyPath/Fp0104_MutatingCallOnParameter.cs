using System;
using System.Collections.Immutable;

namespace Demo.Fp0104_MutatingCallOnParameter;

[Fprog.FunctionalCore]
public static class Prices
{
    public static ImmutableArray<int> AddSorted(ImmutableArray<int> items, int price)
        => items.Add(price).Sort();

    public static int[] Sorted(int[] items)
    {
        int[] ordered = [.. items];
        Array.Sort(ordered);
        return ordered;
    }
}
