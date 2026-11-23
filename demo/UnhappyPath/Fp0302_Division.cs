namespace Demo.Fp0302_Division;

[Fprog.FunctionalCore]
public static class Sharing
{
    public static (long Each, long Leftover) Divide(int total, int recipients)
        => ((long)total / recipients, (long)total % recipients); // expect: FP0302, FP0302
}
