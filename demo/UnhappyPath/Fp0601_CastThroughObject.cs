namespace Demo.Fp0601_CastThroughObject;

[Fprog.FunctionalCore]
public static class Generic
{
    public static string Text<T>(T value) where T : notnull
        => (string)(object)value; // expect: FP0601

    public static T Read<T>(object value) => (T)value; // expect: FP0601

    public static int Number<T>(T value)
    {
        object? boxed = value;
        return boxed is int number ? number : 0; // expect: FP0601
    }
}
