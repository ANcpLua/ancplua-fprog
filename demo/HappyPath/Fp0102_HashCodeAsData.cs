namespace Demo.Fp0102_HashCodeAsData;

[Fprog.FunctionalCore]
public static class Routing
{
    public static int Shard(string key)
    {
        var hash = 17;
        foreach (var character in key)
            hash = unchecked(hash * 31 + character);

        return (hash & int.MaxValue) % 16;
    }
}
