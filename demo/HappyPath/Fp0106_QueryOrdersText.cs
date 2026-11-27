using System;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0106_QueryOrdersText;

[Fprog.FunctionalCore]
public static class Directory
{
    public sealed record Entry(string City, string Name);

    public static ImmutableArray<Entry> Sort(ImmutableArray<Entry> entries)
        => [.. entries.OrderBy(entry => entry.City, StringComparer.Ordinal)
                      .ThenByDescending(entry => entry.Name, StringComparer.Ordinal)];
}
