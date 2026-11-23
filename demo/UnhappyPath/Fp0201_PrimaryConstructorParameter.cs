namespace Demo.Fp0201_PrimaryConstructorParameter;

// Bug: a primary constructor parameter is a variable that every member can change. It cannot be readonly,
// so this "core" class has hidden state: Next() returns 1, 2, 3 for the same call.
[Fprog.FunctionalCore]
public sealed class InvoiceNumbers(int last) // expect: FP0201
{
    public int Next() => ++last;
}

[Fprog.FunctionalCore]
public struct Budget(decimal remaining, string owner) // expect: FP0201
{
    public string Owner => owner;

    public decimal Spend(decimal amount)
    {
        remaining -= amount;
        return remaining;
    }
}
