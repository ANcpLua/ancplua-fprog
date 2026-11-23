using System;
using System.Globalization;

namespace Demo.Fp0102_LocalTimeZone;

[Fprog.FunctionalCore]
public static class Schedule
{
    public static TimeSpan Offset() => TimeZoneInfo.Local.BaseUtcOffset; // expect: FP0102
    public static DateTime Local(DateTime utc) => utc.ToLocalTime(); // expect: FP0102
    public static string CultureName() => CultureInfo.CurrentCulture.Name; // expect: FP0102
}
