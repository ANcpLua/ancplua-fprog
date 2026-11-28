using System.Collections.Immutable;

namespace Demo.Fp0303_FlagTuple;

[Fprog.FunctionalCore]
public static class Finder
{
    // D1: a caller can read Name without reading Found.
    public static (bool Found, string Name) Find(ImmutableArray<string> names, string wanted)
        => names.Contains(wanted) ? (true, wanted) : (false, "");
}
