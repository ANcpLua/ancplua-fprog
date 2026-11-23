namespace Demo.Fp0107_PointersInCore;

[Fprog.FunctionalCore]
public static class Cells
{
    public static int Replacement() => 42;

    public static int[] SetFirst(int[] values)
    {
        int[] updated = [.. values];
        if (updated.Length > 0)
            updated[0] = 7;
        return updated;
    }

    public static int Copy(int value) => value;

    public static bool IsEmpty(int[] values) => values.Length == 0;
}
