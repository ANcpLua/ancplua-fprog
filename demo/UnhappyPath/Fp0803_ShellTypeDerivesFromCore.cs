using System;

namespace Demo.Fp0803_ShellTypeDerivesFromCore;

[Fprog.FunctionalCore]
public closed class Rule
{
    public abstract bool Applies(DateTime when);
}

public sealed class TodayRule : Rule // expect: FP0803
{
    public override bool Applies(DateTime when) => DateTime.UtcNow.Date == when.Date;
}

[Fprog.FunctionalCore]
public static class Orders
{
    public static bool Check(Rule rule, DateTime when) => rule.Applies(when);
}
