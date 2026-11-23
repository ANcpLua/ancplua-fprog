using System.Collections.Generic;

namespace Demo.Fp0104_ElementHandedOutByCall;

[Fprog.FunctionalCore]
public static class Orders
{
    public static List<int> AddToFirst(List<List<int>> orders, int item)
    {
        if (orders.Find(order => order.Count > 0) is not { } selected)
            return [];

        selected.Add(item); // expect: FP0104
        return selected;
    }

    public static List<int> AddById(IDictionary<int, List<int>> byId, int id, int item)
    {
        if (!byId.TryGetValue(id, out var selected))
            return [];

        selected.Add(item); // expect: FP0104
        return selected;
    }
}
