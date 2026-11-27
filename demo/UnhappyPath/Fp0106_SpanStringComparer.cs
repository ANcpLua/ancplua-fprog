using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0106_SpanStringComparer;

[Fprog.FunctionalCore]
public static class History
{
    public static ImmutableArray<string> ByName(ImmutableArray<string> names)
    {
        var copy = names.ToArray();
        copy.AsSpan().Sort(Comparer<string>.Default); // expect: FP0106
        return [.. copy];
    }
}
