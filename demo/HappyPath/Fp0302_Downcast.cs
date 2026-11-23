namespace Demo.Fp0302_Downcast;

[Fprog.FunctionalCore]
public static class Invoice
{
    public interface ICharge;

    public sealed record Fixed(decimal Amount) : ICharge;

    public sealed record Waived(string Reason) : ICharge;

    public static decimal Total(ICharge charge)
        => charge is Fixed fixedCharge ? fixedCharge.Amount : 0m;
}
