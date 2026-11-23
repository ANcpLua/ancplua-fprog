using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0106_SortingStringsByCulture;

[Fprog.FunctionalCore]
public static class History
{
    public static ImmutableArray<string> Order(ImmutableArray<string> names)
        => [.. names.OrderBy(name => name)]; // expect: FP0106
}
