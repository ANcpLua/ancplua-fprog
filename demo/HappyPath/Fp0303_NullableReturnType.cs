namespace Demo.Fp0303_NullableReturnType;

[Fprog.FunctionalCore]
public static class Finder
{
    public closed record SearchResult
    {
        public sealed record Found(string Name) : SearchResult;

        public sealed record Missing : SearchResult;
    }

    public static SearchResult ViaLocal(bool found)
        => found ? new SearchResult.Found("x") : new SearchResult.Missing();

    public static SearchResult ViaConstant() => new SearchResult.Missing();

    public static (SearchResult Name, int Age) InTuple() => (new SearchResult.Missing(), 0);

    public static int ForcedLength(string? name) => name?.Length ?? 0;
}
