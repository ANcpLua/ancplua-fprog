using System;
using System.Diagnostics.CodeAnalysis;

namespace Demo.Fp0301_DoesNotReturn;

[Fprog.FunctionalCore]
public static class Validation
{
    public static int Positive(int value) => value > 0 ? value : Reject(value); // expect: FP0301

    [DoesNotReturn]
    private static int Reject(int value)
        => throw new ArgumentOutOfRangeException(nameof(value)); // expect: FP0301
}
