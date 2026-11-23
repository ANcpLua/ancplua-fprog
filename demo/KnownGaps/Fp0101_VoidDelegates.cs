using System;
using System.Collections.Immutable;

namespace Demo.KnownGaps.Fp0101_VoidDelegates;

// Not reported today: FP0101 reports void methods, not functions of type Action that exist only for an effect.
[Fprog.FunctionalCore]
public static class Notify
{
    public static int Each(ImmutableArray<int> items, Action<int> sink)
    {
        foreach (var item in items)
            sink(item);
        return items.Length;
    }

    public static Action<string> Logger() => static _ => { };
}
