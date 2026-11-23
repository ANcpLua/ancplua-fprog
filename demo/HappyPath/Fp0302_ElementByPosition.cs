using System.Collections.Immutable;

namespace Demo.Fp0302_ElementByPosition;

[Fprog.FunctionalCore]
public static class Selection
{
    public static (bool HasValue, int Value) Head(ImmutableArray<int> values)
        => values.IsDefaultOrEmpty ? (false, 0) : (true, values[0]);
}
