namespace Demo.Fp0302_Division;

[Fprog.FunctionalCore]
public static class Sharing
{
    public static (bool CanShare, long Each, long Leftover) Divide(int total, int recipients)
        => recipients > 0 ? (true, (long)total / recipients, (long)total % recipients) : (false, 0, total);
}
