namespace Demo.Fp0107_PointersInCore;

[Fprog.FunctionalCore]
public static class Cells
{
    public static unsafe int Overwrite(int* target) // expect: FP0107, FP0107
    {
        *target = 42;
        target[0] = 42;
        return *target;
    }

    public static int SetFirst(int[] values)
    {
        if (values.Length == 0)
            return 0;

        unsafe // expect: FP0107
        {
            fixed (int* first = values) // expect: FP0107, FP0107
                first[0] = 7;
        }

        return values.Length;
    }

    public static unsafe int Copy(int value) => *(&value); // expect: FP0107, FP0107

    public static unsafe bool IsEmpty() // expect: FP0107
    {
        int* missing = null; // expect: FP0107
        return missing == null;
    }
}
