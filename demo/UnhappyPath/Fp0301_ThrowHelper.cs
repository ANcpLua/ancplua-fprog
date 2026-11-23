using System;

namespace Demo.Fp0301_ThrowHelper;

// Bug: these calls throw exactly like `throw`, only without the keyword. .NET's own code fixes (CA1510 to
// CA1513) turn `if (x is null) throw ...` into such helpers, which makes FP0301 fall silent.
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
