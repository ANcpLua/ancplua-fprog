namespace Demo.Fp0301_DoesNotReturn;

[Fprog.FunctionalCore]
public static class Validation
{
    public static (bool IsValid, int Value) Positive(int value)
        => value > 0 ? (true, value) : (false, 0);
}
