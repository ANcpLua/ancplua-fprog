using System;

namespace Demo.KnownGaps.Fp0801_CoreCallsShell;

public static class Database
{
    public static int Save(string order)
    {
        System.IO.File.AppendAllText("orders.txt", order);
        return 1;
    }
}

[Fprog.FunctionalCore]
public closed class Rule
{
    public abstract bool Applies(DateTime when);
}

public sealed class TodayRule : Rule
{
    public override bool Applies(DateTime when) => DateTime.Now.Date == when.Date;
}

// Not reported today: the core calls a method of the shell, and it dispatches into an override that is not core.
[Fprog.FunctionalCore]
public static class Orders
{
    public static int Place(string order) => Database.Save(order);

    public static bool Check(Rule rule, DateTime when) => rule.Applies(when);
}
