namespace Demo.Fp0202_SetAccessor;

[Fprog.FunctionalCore]
public sealed class Account
{
    public int Balance { get; set; } // expect: FP0202
}
