using System.Collections.Immutable;

namespace Demo.Fp0302_ElementByPosition;

[Fprog.FunctionalCore]
public static class Selection
{
    public static int Head(ImmutableArray<int> values) => values[0]; // expect: FP0302
}
