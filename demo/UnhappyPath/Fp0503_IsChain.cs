namespace Demo.Fp0503_IsChain;

[Fprog.FunctionalCore]
public static class Fees
{
    public closed record Payment;
    public sealed record Card : Payment;
    public sealed record Cash : Payment;
    public sealed record Voucher : Payment;

    public static decimal Fee(Payment payment)
    {
        if (payment is Card) return 0.3m; // expect: FP0503
        if (payment is Cash) return 0m; // expect: FP0503
        return 0m;
    }
}
