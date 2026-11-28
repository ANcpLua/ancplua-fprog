using System.Collections.Immutable;

namespace Demo.Fp0102_HashOrder;

// String hash codes change from process to process, and these collections enumerate in hash order.
[Fprog.FunctionalCore]
public static class Listing
{
    // C9: three runs gave three orders.
    public static string Joined(ImmutableHashSet<string> names) => string.Join(",", names);

    // C10: the first key differs from run to run.
    public static string FirstKey(ImmutableDictionary<string, int> prices)
    {
        foreach (var pair in prices)
            return pair.Key;

        return "";
    }
}
