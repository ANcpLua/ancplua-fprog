using System.Globalization;

namespace Demo.Fp0106_FormattingByCulture;

[Fprog.FunctionalCore]
public static class Invoice
{
    public static string Price(decimal amount) => amount.ToString("C", CultureInfo.InvariantCulture);
    public static string Summary(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"Pay {amount:C}");
}
