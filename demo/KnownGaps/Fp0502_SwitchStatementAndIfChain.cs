namespace Demo.KnownGaps.Fp0502_SwitchStatementAndIfChain;

public closed record Payment
{
    public sealed record Card(string Number) : Payment;

    public sealed record Cash(decimal Amount) : Payment;

    public sealed record Voucher(string Code) : Payment;
}

// Not reported today: FP0502 checks switch expressions only, and FP0501 knows no if/else.
[Fprog.FunctionalCore]
public static class Fees
{
    public static decimal Statement(Payment payment)
    {
        switch (payment)
        {
            case Payment.Card: return 0.3m;
            case Payment.Cash: return 0m;
        }

        return 0m;                                   // Voucher lands here
    }

    public static decimal Negated(Payment payment)
    {
        switch (payment)
        {
            case Payment.Card: return 0.3m;
            case not Payment.Card: return 0m;        // every other case, also future ones
        }
    }

    public static decimal IfChain(Payment payment)
    {
        if (payment is Payment.Card) return 0.3m;
        if (payment is Payment.Cash) return 0m;
        return 0m;                                   // Voucher lands here
    }
}
