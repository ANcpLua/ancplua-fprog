using System.Collections.Generic;
using System.Collections.Immutable;

namespace Demo.Fp0104_ReferenceInsideValueParameter;

[Fprog.FunctionalCore]
public static class OrderChanges
{
    public sealed class Order(List<int> lines)
    {
        public List<int> Lines { get; } = lines;
    }

    public static ImmutableArray<Order> AddToFirst(ImmutableArray<Order> orders, int line)
    {
        if (orders.IsDefaultOrEmpty)
            return orders;

        var first = orders[0];
        first.Lines.Add(line); // expect: FP0104
        return orders;
    }
}
