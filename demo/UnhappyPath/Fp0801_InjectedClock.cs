using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Demo.Fp0801_InjectedClock;

[Fprog.FunctionalCore]
public static class Deadline
{
    public static bool IsLate(DateTimeOffset due, TimeProvider clock) => clock.GetUtcNow() > due; // expect: FP0801
    public static long Stamp() => Stopwatch.GetTimestamp(); // expect: FP0801
    public static int Dice() => RandomNumberGenerator.GetInt32(1, 7); // expect: FP0801
}
