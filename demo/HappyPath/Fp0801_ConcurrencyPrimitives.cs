using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0801_ConcurrencyPrimitives;

[Fprog.FunctionalCore]
public static class Compute
{
    public static ImmutableArray<int> Ordered(ImmutableArray<int> values)
        => [.. values.Select(value => value * 2)];

    public static int Guarded(int requestValue) => requestValue;

    public static bool ShouldQueue(ImmutableArray<int> values) => !values.IsEmpty;
}
