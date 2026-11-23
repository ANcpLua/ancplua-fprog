using System;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0302_EmptySequence;

[Fprog.FunctionalCore]
public static class Statistics
{
    public static (bool HasValue, int Value) Largest(ImmutableArray<int> values)
        => values.IsDefaultOrEmpty ? (false, 0) : (true, values.Aggregate(int.MinValue, Math.Max));

    public static (bool HasValue, double Value) Mean(ImmutableArray<int> values)
        => values.IsDefaultOrEmpty ? (false, 0) : (true, values.Sum(static value => (double)value) / values.Length);
}
