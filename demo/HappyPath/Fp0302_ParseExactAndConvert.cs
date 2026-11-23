using System;
using System.Globalization;

namespace Demo.Fp0302_ParseExactAndConvert;

[Fprog.FunctionalCore]
public static class ImportDates
{
    public static (bool Parsed, DateTime Value) Year(string text)
        => DateTime.TryParseExact(text, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var year)
            ? (true, year)
            : (false, DateTime.UnixEpoch);

    public static (bool Parsed, int Value) Quantity(string text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            ? (true, quantity)
            : (false, 0);
}
