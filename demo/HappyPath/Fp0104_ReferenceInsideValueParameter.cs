using System.Collections.Immutable;

namespace Demo.Fp0104_ReferenceInsideValueParameter;

[Fprog.FunctionalCore]
public static class OrderChanges
{
    public sealed record Order(ImmutableList<int> Lines);

    public static ImmutableArray<Order> AddToFirst(ImmutableArray<Order> orders, int line)
    {
        if (orders.IsDefaultOrEmpty)
            return orders;

        var first = orders[0];
        return orders.SetItem(0, first with { Lines = first.Lines.Add(line) });
    }
}
