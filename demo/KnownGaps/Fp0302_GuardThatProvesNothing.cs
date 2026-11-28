using System.Collections.Immutable;

namespace Demo.Fp0302_GuardThatProvesNothing;

// Each test mentions the divisor, the index or the length, so FP0302 takes it for a guard.
// None of them keeps the failing value away from the operation.
[Fprog.FunctionalCore]
public static class Guards
{
    // A1: divides exactly when the divisor is zero.
    public static int WrongBranch(int amount, int parts) => parts == 0 ? amount / parts : 0;

    // A2: the test has an empty body and protects nothing.
    public static int EmptyTest(int amount, int parts)
    {
        if (parts > 100) { }
        return amount / parts;
    }

    // A3: reads exactly when the index is out of range.
    public static int WrongIndexBranch(ImmutableArray<int> values, int index)
        => index >= values.Length ? values[index] : 0;

    // A4: reads the first element exactly when there is none.
    public static int WrongEmptyBranch(ImmutableArray<int> values) => values.IsEmpty ? values[0] : 0;

    // A5: the test is on another index.
    public static int OtherIndex(ImmutableArray<int> values, int checkedIndex, int usedIndex)
        => checkedIndex < values.Length ? values[usedIndex] : 0;
}
