using System;
using System.Collections.Immutable;

namespace Demo.Fp0101_VoidDelegates;

[Fprog.FunctionalCore]
public static class Feedback
{
    public static int Each(ImmutableArray<int> items, Action<int> sink) // expect: FP0101
    {
        foreach (var item in items)
            sink(item);

        return items.Length;
    }

    public static Action<string> Describe() => static line => { }; // expect: FP0101
}
