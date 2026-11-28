using System;
using System.Diagnostics.CodeAnalysis;

namespace Demo.Fp0301_SilencedBySuppressMessage;

[Fprog.FunctionalCore]
public static class Checks
{
    // D4: one attribute, and the finding on the throw is gone.
    [SuppressMessage("Functional", "FP0301")]
    public static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
