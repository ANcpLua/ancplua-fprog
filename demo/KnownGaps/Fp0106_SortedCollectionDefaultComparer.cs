using System.Collections.Immutable;

namespace Demo.Fp0106_SortedCollectionDefaultComparer;

// Without a comparer these collections sort strings by the current culture. The letters z, a-umlaut, a, B
// come out as a,B,z,a-umlaut under sv-SE and as a,a-umlaut,B,z under de-DE.
[Fprog.FunctionalCore]
public static class Glossary
{
    // S1
    public static ImmutableSortedSet<string> Sorted(ImmutableArray<string> names) => names.ToImmutableSortedSet();

    // S2
    public static ImmutableSortedDictionary<string, int> Lengths(ImmutableArray<string> names)
        => names.ToImmutableSortedDictionary(static name => name, static name => name.Length);

    // S3
    public static ImmutableSortedSet<string> Letters() => ImmutableSortedSet.Create("z", "ä", "a", "B");
}
