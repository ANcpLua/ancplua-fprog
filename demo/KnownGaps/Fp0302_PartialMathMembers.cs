using System;

namespace Demo.Fp0302_PartialMathMembers;

[Fprog.FunctionalCore]
public static class Bounds
{
    // B2: ArgumentException when low is greater than high.
    public static int Limit(int value, int low, int high) => Math.Clamp(value, low, high);

    // B3: DivideByZeroException when parts is zero. The '/' operator is reported, this method is not.
    public static (int Quotient, int Remainder) Share(int total, int parts) => Math.DivRem(total, parts);
}
