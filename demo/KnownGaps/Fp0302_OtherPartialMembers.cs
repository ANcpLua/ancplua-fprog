using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Demo.KnownGaps.Fp0302_OtherPartialMembers;

// Not reported today: each member throws for some input, and none is in the FP0302 list.
[Fprog.FunctionalCore]
public static class Partial
{
    public static DateTime Year(string text) => DateTime.ParseExact(text, "yyyy", CultureInfo.InvariantCulture);
    public static int Number(string text) => Convert.ToInt32(text, CultureInfo.InvariantCulture);
    public static int Largest(ImmutableArray<int> values) => values.Max();                 // empty: InvalidOperationException
    public static double Mean(ImmutableArray<int> values) => values.Average();             // empty: InvalidOperationException
    public static int Head(ImmutableArray<int> values) => values[0];
    public static int Ratio(int a, int b) => a / b;
    public static T Cast<T>(object value) => (T)value;
}
