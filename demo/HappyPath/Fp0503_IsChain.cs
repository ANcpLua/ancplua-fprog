namespace Demo.Fp0503_IsChain;

[Fprog.FunctionalCore]
public static class Fees
{
    public closed record Payment;
    public sealed record Card : Payment;
    public sealed record Cash : Payment;
    public sealed record Voucher : Payment;

    public static decimal Fee(Payment payment) => payment switch
    {
        Card => 0.3m,
        Cash => 0m,
        Voucher => 0.1m,
    };
}
