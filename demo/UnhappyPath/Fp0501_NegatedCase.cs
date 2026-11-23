namespace Demo.Fp0501_NegatedCase;

[Fprog.FunctionalCore]
public static class Fees
{
    public closed record Payment;
    public sealed record Card : Payment;
    public sealed record Cash : Payment;
    public sealed record Voucher : Payment;

    public static decimal Fee(Payment payment)
    {
        switch (payment)
        {
            case Card: return 0.3m;
            case not Card: return 0m; // expect: FP0501
        }
    }
}
