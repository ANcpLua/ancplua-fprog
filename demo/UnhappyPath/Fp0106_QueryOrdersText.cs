using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0106_QueryOrdersText;

[Fprog.FunctionalCore]
public static class Directory
{
    public sealed record Entry(string City, string Name);

    public static ImmutableArray<Entry> Sort(ImmutableArray<Entry> entries)
        => [.. from entry in entries
               orderby entry.City, // expect: FP0106
                       entry.Name descending // expect: FP0106
               select entry];
}
