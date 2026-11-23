using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Demo.Fp0302_DictionaryIndexer;

// Correct: every lookup says what happens for a missing key.
[Fprog.FunctionalCore]
public static class Prices
{
    public static decimal FromImmutable(ImmutableDictionary<string, decimal> prices, string sku)
        => prices.TryGetValue(sku, out var price) ? price : 0m;

    public static decimal FromFrozen(FrozenDictionary<string, decimal> prices, string sku)
        => prices.TryGetValue(sku, out var price) ? price : 0m;

    public static decimal FromSorted(ImmutableSortedDictionary<string, decimal> prices, string sku)
        => prices.GetValueOrDefault(sku, 0m);

    public static decimal FromInterface(IReadOnlyDictionary<string, decimal> prices, string sku)
        => prices.TryGetValue(sku, out var price) ? price : 0m;

    public static bool Has(ImmutableDictionary<string, decimal> prices, string sku) => prices.ContainsKey(sku);
}
