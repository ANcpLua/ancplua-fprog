using System;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0101_VoidDelegates;

[Fprog.FunctionalCore]
public static class Feedback
{
    public static ImmutableArray<TResult> Each<TResult>(ImmutableArray<int> items, Func<int, TResult> describe)
        => [.. items.Select(describe)];

    public static Func<string, string> Describe() => static line => "received " + line;
}
