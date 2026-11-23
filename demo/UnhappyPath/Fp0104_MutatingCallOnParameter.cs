using System;
using System.Collections.Generic;

namespace Demo.Fp0104_MutatingCallOnParameter;

[Fprog.FunctionalCore]
public static class Prices
{
    public static List<int> AddSorted(List<int> items, int price)
    {
        items.Add(price); // expect: FP0104
        items.Sort(); // expect: FP0104
        return items;
    }

    public static int[] Sorted(int[] items)
    {
        Array.Sort(items); // expect: FP0104
        return items;
    }
}
