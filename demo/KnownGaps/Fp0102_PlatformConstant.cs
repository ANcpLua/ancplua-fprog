using System;

namespace Demo.Fp0102_PlatformConstant;

[Fprog.FunctionalCore]
public static class Bytes
{
    // C7: depends on the processor the program runs on.
    public static bool LowByteFirst() => BitConverter.IsLittleEndian;
}
