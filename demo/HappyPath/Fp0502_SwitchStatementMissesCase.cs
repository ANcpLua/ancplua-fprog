namespace Demo.Fp0502_SwitchStatementMissesCase;

[Fprog.FunctionalCore]
public static class Fees
{
    public static decimal Fee(Payment payment)
    {
        switch (payment)
        {
            case Card: return 0.3m;
            case Cash: return 0m;
            case Voucher: return 0.1m;
        }

        return 0m;
    }
}
