using System.Collections.Generic;

namespace Demo.Fp0104_ConvertedBranches;

// The write below changes the caller's array or list. See FINDINGS.md.
[Fprog.FunctionalCore]
public static class Scores
{
    public static int Reset(int[] fixedScores, List<int> openScores, bool useFixed)
    {
        (IList<int> Values, int Kind) chosen = useFixed ? (fixedScores, 0) : (openScores, 1);
        chosen.Values[0] = 0;
        return chosen.Kind;
    }
}
