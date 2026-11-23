using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0302_EmptySequence;

[Fprog.FunctionalCore]
public static class Statistics
{
    public static int Largest(ImmutableArray<int> values) => values.Max(); // expect: FP0302

    public static double Mean(ImmutableArray<int> values) => values.Average(); // expect: FP0302
}
