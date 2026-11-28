using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Demo.Fp0302_PartialTextAndRangeMembers;

[Fprog.FunctionalCore]
public static class Text
{
    // B4: ArgumentOutOfRangeException when start lies beyond the text.
    public static string Tail(string text, int start) => text.Substring(start);

    // B12: ArgumentOutOfRangeException when count is negative.
    public static IEnumerable<int> FirstNumbers(int count) => Enumerable.Range(0, count);

    // B13: FormatException when the format names an argument that is not there, as in "{1}".
    public static string Fill(string format, int value) => string.Format(CultureInfo.InvariantCulture, format, value);
}
