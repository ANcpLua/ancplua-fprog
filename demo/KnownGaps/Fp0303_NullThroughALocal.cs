namespace Demo.KnownGaps.Fp0303_NullThroughALocal;

// Not reported today: FP0303 reports the literal in the return, not a null that travels through a name.
[Fprog.FunctionalCore]
public static class Finder
{
    private const string? Nothing = null;

    public static string? ViaLocal(bool found)
    {
        string? result = null;
        if (found) result = "x";
        return result;
    }

    public static string? ViaConstant() => Nothing;

    public static (string? Name, int Age) InTuple() => (null, 0);
}
