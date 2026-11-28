using System;

namespace Demo.Fp0102_TimeZoneBehindConversion;

// Each result follows the time zone of the machine.
// For 2026-07-01 12:00 the offset is 02:00 with TZ=Europe/Vienna and 00:00 with TZ=UTC.
[Fprog.FunctionalCore]
public static class Clock
{
    // C1
    public static DateTimeOffset WithOffset(DateTime time) => new DateTimeOffset(time);

    // C2: the same conversion, implicit.
    public static DateTimeOffset Converted(DateTime time) => time;

    // C3
    public static bool IsSummerTime(DateTime time) => time.IsDaylightSavingTime();

    // C4
    public static DateTime ToUtc(DateTime time) => TimeZoneInfo.ConvertTimeToUtc(time);

    // C5: reads the machine's time-zone database.
    public static DateTime InVienna(DateTime time) => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Europe/Vienna");
}
