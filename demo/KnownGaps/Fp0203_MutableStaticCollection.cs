using System.Collections.Generic;

namespace Demo.Fp0203_MutableStaticCollection;

// FP0203 looks at records and readonly structs. The fields are readonly, so FP0201 and FP0102 pass them too,
// and any code outside the core can change what the core reads.
[Fprog.FunctionalCore]
public static class Registry
{
    // C0
    public static readonly List<int> Shared = new();

    // E2
    public static readonly Dictionary<string, int> Prices = new();

    // C8
    public static int SharedCount() => Shared.Count;

    // E3
    public static int Price(string name) => Prices.TryGetValue(name, out var price) ? price : 0;
}
