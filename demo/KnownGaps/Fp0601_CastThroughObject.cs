namespace Demo.KnownGaps.Fp0601_CastThroughObject;

// Not reported today: a hard cast and a test through an object local inspect T just like `is` does.
[Fprog.FunctionalCore]
public static class Generic
{
    public static string HardCast<T>(T value) where T : notnull => (string)(object)value;

    public static int ViaLocal<T>(T value)
    {
        object? boxed = value;
        return boxed is int number ? number : 0;
    }
}
