using System.Collections.Generic;
using System.Collections.Immutable;

namespace Demo.Fp0104_ElementHandedOutByCall;

[Fprog.FunctionalCore]
public static class Orders
{
    public static ImmutableArray<int> AddToFirst(List<List<int>> orders, int item)
        => orders.Find(order => order.Count > 0) is { } selected ? [.. selected, item] : [];

    public static ImmutableArray<int> AddById(IDictionary<int, List<int>> byId, int id, int item)
        => byId.TryGetValue(id, out var selected) ? [.. selected, item] : [];
}
