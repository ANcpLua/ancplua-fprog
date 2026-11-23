using System.Collections.Generic;

namespace Demo.Fp0104_AliasOfParameter;

public sealed class Line
{
    public int Quantity;
}

public sealed class Order
{
    public int Total;
    public Line? First;
    public List<Line> Lines = new();
}

// Bug: each write goes to the caller's objects, only through another name.
[Fprog.FunctionalCore]
public static class Normalizer
{
    public static int ViaLocal(Order order)
    {
        var same = order;
        same.Total = 0; // expect: FP0104
        return same.Total;
    }

    public static int ViaMember(Order order)
    {
        var first = order.First;
        if (first is null) return 0;
        first.Quantity++; // expect: FP0104
        return first.Quantity;
    }

    public static int ViaForeach(Order order)
    {
        foreach (var line in order.Lines)
            line.Quantity = 0; // expect: FP0104
        return order.Lines.Count;
    }

    public static int ViaPattern(object boxed)
    {
        if (boxed is Order order)
            order.Total = 1; // expect: FP0104
        return 0;
    }
}
