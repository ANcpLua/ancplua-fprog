namespace Demo.Fp0502_SwitchStatementMissesCase;

[Fprog.FunctionalCore]
public static class Fees
{
    public closed record Payment;
    public sealed record Card : Payment;
    public sealed record Cash : Payment;
    public sealed record Voucher : Payment;

    public static decimal Fee(Payment payment)
    {
        switch (payment) // expect: FP0502
        {
            case Card: return 0.3m;
            case Cash: return 0m;
        }

        return 0m;
    }
}
