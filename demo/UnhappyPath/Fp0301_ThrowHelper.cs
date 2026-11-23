using System;

namespace Demo.Fp0301_ThrowHelper;

// These APIs throw on invalid input. Their names hide the same failure that a throw statement would expose.
[Fprog.FunctionalCore]
public static class Guard
{
    public static int Positive(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); // expect: FP0301
        return value;
    }

    public static string Named(string? name)
    {
        ArgumentNullException.ThrowIfNull(name); // expect: FP0301
        ArgumentException.ThrowIfNullOrWhiteSpace(name); // expect: FP0301
        return name;
    }

    public static int Keyword(int value)
        => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); // expect: FP0301
}
