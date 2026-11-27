using System;

namespace Demo.Fp0302_ScalarConvertOverloads;

[Fprog.FunctionalCore]
public static class Quantities
{
    public static int AsInt32(byte value) => Convert.ToInt32(value);

    public static Func<byte, int> Converter() => Convert.ToInt32;
}
