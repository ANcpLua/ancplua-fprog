using System.Collections.Generic;

namespace Demo.Fp0105_StateHeldInAField;

// Bug: the fields are readonly, but the collections behind them change with every call. Price("a", 10) followed
// by Price("a", 20) returns the first price twice, and two threads calling it corrupt the dictionary.
[Fprog.FunctionalCore]
public static class Pricing
{
    private static readonly Dictionary<string, decimal> Cache = new();

    public static decimal Price(string sku, decimal basePrice)
    {
        if (Cache.TryGetValue(sku, out var cached)) return cached;
        var price = basePrice * 1.2m;
        Cache[sku] = price; // expect: FP0105
        return price;
    }
}

// Bug: Add is not idempotent. The same call twice counts the item twice.
[Fprog.FunctionalCore]
public sealed class Basket
{
    private readonly List<string> _items = new();
    private readonly int[] _totals = new int[1];

    public int Add(string item)
    {
        _items.Add(item); // expect: FP0105
        return _items.Count;
    }

    public int Bump()
    {
        if (_totals.Length == 0) return 0;
        return ++_totals[0]; // expect: FP0105
    }
}
