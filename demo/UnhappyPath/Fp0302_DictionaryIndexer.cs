using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Demo.Fp0302_DictionaryIndexer;

// Bug: the indexer throws KeyNotFoundException for a missing key. A price lookup that fails for one unknown
// article takes the whole calculation down instead of returning "unknown".
[Fprog.FunctionalCore]
public static class Prices
{
    public static decimal FromImmutable(ImmutableDictionary<string, decimal> prices, string sku)
        => prices[sku]; // expect: FP0302

    public static decimal FromFrozen(FrozenDictionary<string, decimal> prices, string sku)
        => prices[sku]; // expect: FP0302

    public static decimal FromSorted(ImmutableSortedDictionary<string, decimal> prices, string sku)
        => prices[sku]; // expect: FP0302

    public static decimal FromInterface(IReadOnlyDictionary<string, decimal> prices, string sku)
        => prices[sku]; // expect: FP0302
}
