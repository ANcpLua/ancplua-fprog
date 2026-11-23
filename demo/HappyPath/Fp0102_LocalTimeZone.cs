using System;
using System.Globalization;

namespace Demo.Fp0102_LocalTimeZone;

[Fprog.FunctionalCore]
public static class Schedule
{
    public static TimeSpan Offset(TimeZoneInfo zone) => zone.BaseUtcOffset;
    public static DateTime Local(DateTime utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
    public static string CultureName(CultureInfo culture) => culture.Name;
}
