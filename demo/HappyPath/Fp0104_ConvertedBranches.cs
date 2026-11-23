using System.Collections.Immutable;

namespace Demo.Fp0104_ConvertedBranches;

// Correct: the chosen scores come back as a new array; the caller's collections stay as they were.
[Fprog.FunctionalCore]
public static class Scores
{
    public static ImmutableArray<int> Reset(ImmutableArray<int> fixedScores, ImmutableArray<int> openScores, bool useFixed)
    {
        var chosen = useFixed ? fixedScores : openScores;
        return chosen.IsEmpty ? chosen : chosen.SetItem(0, 0);
    }
}
