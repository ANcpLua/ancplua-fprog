using System;

namespace Demo.Fp0803_ShellTypeDerivesFromCore;

[Fprog.FunctionalCore]
public closed class Rule
{
    public abstract bool Applies(DateTime when);
}

[Fprog.FunctionalCore]
public sealed class TodayRule(DateTime today) : Rule
{
    public override bool Applies(DateTime when) => today.Date == when.Date;
}

[Fprog.FunctionalCore]
public static class Orders
{
    public static bool Check(Rule rule, DateTime when) => rule.Applies(when);
}
