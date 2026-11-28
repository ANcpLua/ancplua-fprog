namespace Demo.Fp0302_SilencedByPragma;

[Fprog.FunctionalCore]
public static class Sharing
{
    // D3: one directive, and the finding on the division is gone.
#pragma warning disable FP0302
    public static int Each(int total, int parts) => total / parts;
#pragma warning restore FP0302
}
