namespace Demo.Fp0106_FormattingByCulture;

[Fprog.FunctionalCore]
public static class Invoice
{
    public static string Price(decimal amount) => amount.ToString("C"); // expect: FP0106
    public static string Summary(decimal amount) => $"Pay {amount:C}"; // expect: FP0106
}
