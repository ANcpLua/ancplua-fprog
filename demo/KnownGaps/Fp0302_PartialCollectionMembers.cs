using System.Collections.Immutable;

namespace Demo.Fp0302_PartialCollectionMembers;

[Fprog.FunctionalCore]
public static class Catalog
{
    // B5: ArgumentException when a key occurs twice.
    public static ImmutableDictionary<string, int> Lengths(ImmutableArray<string> names)
        => names.ToImmutableDictionary(static name => name, static name => name.Length);

    // B6: ArgumentOutOfRangeException when index lies outside the array.
    public static ImmutableArray<int> Without(ImmutableArray<int> values, int index) => values.RemoveAt(index);

    // B7: ArgumentException when the key is there with another value.
    public static ImmutableDictionary<string, int> Register(ImmutableDictionary<string, int> prices, string name)
        => prices.Add(name, 1);
}
