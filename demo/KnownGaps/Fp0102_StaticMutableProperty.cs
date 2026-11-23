namespace Demo.KnownGaps.Fp0102_StaticMutableProperty;

public static class Shell
{
    public static int Counter { get; set; }
}

// Not reported today: FP0102 names static fields, and this is the same state behind a property.
[Fprog.FunctionalCore]
public static class Numbering
{
    public static int Next() => Shell.Counter + 1;
}
