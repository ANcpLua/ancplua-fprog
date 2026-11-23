using System.Collections.Generic;
using System.Collections.Immutable;

namespace Demo.Fp0105_StateHeldInAField;

// Correct: the table never changes after the type is initialized.
[Fprog.FunctionalCore]
public static class Pricing
{
    private static readonly ImmutableDictionary<string, decimal> Rates =
        ImmutableDictionary<string, decimal>.Empty.Add("standard", 1.2m).Add("reduced", 1.1m);

    public static decimal Price(string kind, decimal basePrice)
        => Rates.TryGetValue(kind, out var rate) ? basePrice * rate : basePrice;
}

// Correct: Add returns a new basket; the array is filled while the object is built and never afterwards.
[Fprog.FunctionalCore]
public sealed class Basket
{
    private readonly ImmutableList<string> _items;
    private readonly int[] _lengths;

    public Basket(ImmutableList<string> items)
    {
        _items = items;
        _lengths = new int[items.Count];
        for (var index = 0; index < items.Count; index++)
            _lengths[index] = items[index].Length;
    }

    public int Count => _items.Count;

    public Basket Add(string item) => new(_items.Add(item));

    public int Longest()
    {
        var seen = new List<int>();
        foreach (var length in _lengths)
            seen.Add(length);
        seen.Sort();
        return seen.Count == 0 ? 0 : seen[^1];
    }
}
