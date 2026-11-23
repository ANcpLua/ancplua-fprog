using System;
using System.Globalization;

namespace Demo.KnownGaps.Fp0102_ClockAndRandomnessNotInTheList;

// Not reported today. Each member returns something different for the same arguments, or depends on the machine.
[Fprog.FunctionalCore]
public static class Hidden
{
    public static TimeSpan Offset() => TimeZoneInfo.Local.BaseUtcOffset;                            // machine setting
    public static DateTime Local(DateTime utc) => utc.ToLocalTime();                                // machine setting
    public static string Money(decimal amount) => amount.ToString("C");                             // current culture
    public static string Culture() => CultureInfo.CurrentCulture.Name;                              // ambient state
    public static int Shard(string key, int shards) => Math.Abs(key.GetHashCode() % shards);        // differs per process
}
