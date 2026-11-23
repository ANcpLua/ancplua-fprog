namespace Demo.Fp0301_ThrowHelper;

[Fprog.FunctionalCore]
public closed record Checked
{
    public sealed record Valid(int Value) : Checked;

    public sealed record Invalid(string Reason) : Checked;
}

// Correct: a failed check is a value that the caller has to look at.
[Fprog.FunctionalCore]
public static class Guard
{
    public static Checked Positive(int value)
        => value > 0 ? new Checked.Valid(value) : new Checked.Invalid("must be positive");

    public static string Describe(Checked result) => result switch
    {
        Checked.Valid valid => valid.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Checked.Invalid invalid => invalid.Reason,
    };
}
