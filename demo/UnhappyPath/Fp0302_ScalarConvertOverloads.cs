using System;

namespace Demo.Fp0302_ScalarConvertOverloads;

[Fprog.FunctionalCore]
public static class Quantities
{
    public static int AsInt32(long value) => Convert.ToInt32(value); // expect: FP0302

    public static Func<long, int> Converter() => Convert.ToInt32; // expect: FP0302
}
