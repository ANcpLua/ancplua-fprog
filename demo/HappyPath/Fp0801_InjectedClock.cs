using System;

namespace Demo.Fp0801_InjectedClock;

[Fprog.FunctionalCore]
public static class Deadline
{
    public sealed record Observation(DateTimeOffset Now, long Timestamp, int Dice);

    public static bool IsLate(DateTimeOffset due, Observation observed) => observed.Now > due;
    public static long Stamp(Observation observed) => observed.Timestamp;
    public static int Dice(Observation observed) => observed.Dice;
}
