using System.Globalization;

namespace Demo.Fp0104_OutArgument;

[Fprog.FunctionalCore]
public sealed record Counter(int Value);

// Correct: the result is a local, and the new state is returned.
[Fprog.FunctionalCore]
public static class Reader
{
    public static Counter Read(string text, Counter counter)
        => int.TryParse(text, CultureInfo.InvariantCulture, out var value) ? counter with { Value = value } : counter;

    public static int ReadOrZero(string text)
    {
        int.TryParse(text, CultureInfo.InvariantCulture, out var value);
        return value;
    }
}
