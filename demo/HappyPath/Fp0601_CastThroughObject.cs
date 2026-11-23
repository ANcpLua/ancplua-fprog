using System;

namespace Demo.Fp0601_CastThroughObject;

[Fprog.FunctionalCore]
public static class Generic
{
    public static string Text<T>(T value, Func<T, string> describe) => describe(value);

    public static T Read<T>(T value) => value;

    public static int Number<T>(T value, Func<T, int> number) => number(value);
}
