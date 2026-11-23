using System;
using System.Globalization;

namespace Demo.Fp0302_ParseExactAndConvert;

[Fprog.FunctionalCore]
public static class ImportDates
{
    public static DateTime Year(string text)
        => DateTime.ParseExact(text, "yyyy", CultureInfo.InvariantCulture); // expect: FP0302

    public static int Quantity(string text)
        => Convert.ToInt32(text, CultureInfo.InvariantCulture); // expect: FP0302
}
