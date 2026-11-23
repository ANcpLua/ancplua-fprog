namespace Demo.Fp0104_OutArgument;

[Fprog.FunctionalCore]
public sealed class Counter
{
    public int Value; // expect: FP0201
    public readonly int[] History = new int[4];
}

// Bug: the "pure" function overwrites a field of the caller's object through an out argument. Called twice with
// different texts, the caller's counter silently changes.
[Fprog.FunctionalCore]
public static class Reader
{
    public static bool Read(string text, Counter counter)
        => int.TryParse(text, out counter.Value); // expect: FP0104

    public static bool ReadInto(string text, int[] slots)
        => int.TryParse(text, out slots[0]); // expect: FP0104

    public static int Bump(Counter counter) => Cells.Increment(ref counter.History[0]); // expect: FP0104, FP0801
}

public static class Cells
{
    public static int Increment(ref int cell) => ++cell;
}
