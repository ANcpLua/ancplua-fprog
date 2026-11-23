namespace Demo.Fp0201_PrimaryConstructorParameter;

// Correct: the parameter is only read, and a new value is returned instead of changing the old one.
[Fprog.FunctionalCore]
public sealed class InvoiceNumbers(int last)
{
    public int Last => last;

    public InvoiceNumbers Next() => new(last + 1);
}

[Fprog.FunctionalCore]
public readonly record struct Budget(decimal Remaining, string Owner)
{
    public Budget Spend(decimal amount) => this with { Remaining = Remaining - amount };
}
