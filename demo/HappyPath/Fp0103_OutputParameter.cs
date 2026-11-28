namespace Demo.Fp0103_OutputParameter;

[Fprog.FunctionalCore]
public static class Halves
{
    public static (int Quotient, int Remainder) DivideByTwo(int value) => (value / 2, value % 2);
}
