using System;

namespace Demo.Fp0302_ThrowingConstructors;

[Fprog.FunctionalCore]
public static class Parts
{
    // B8: ArgumentOutOfRangeException for a day such as 30 February.
    public static DateTime Day(int year, int month, int day) => new DateTime(year, month, day);

    // B9: UriFormatException when the text is no URI. Uri.TryCreate is the total sibling.
    public static Uri Link(string text) => new Uri(text);
}
