namespace Demo.Fp0102_HashCodeAsData;

[Fprog.FunctionalCore]
public static class Routing
{
    public static int Shard(string key) => (key.GetHashCode() & int.MaxValue) % 16; // expect: FP0102
}
