namespace Demo.Fp0103_OutputParameter;

[Fprog.FunctionalCore]
public static class Halves
{
    public static int DivideByTwo(int value, out int remainder) // expect: FP0103
    {
        remainder = value % 2;
        return value / 2;
    }
}
