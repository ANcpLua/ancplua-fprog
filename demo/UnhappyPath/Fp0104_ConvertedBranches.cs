using System.Collections.Generic;

namespace Demo.Fp0104_ConvertedBranches;

// Bug: the array or list that the caller passed in is changed in place. The tuple conversion around each
// branch does not hide that.
[Fprog.FunctionalCore]
public static class Scores
{
    public static int Reset(int[] fixedScores, List<int> openScores, bool useFixed)
    {
        (IList<int> Values, int Kind) chosen = useFixed ? (fixedScores, 0) : (openScores, 1);
        chosen.Values[0] = 0; // expect: FP0104
        return chosen.Kind;
    }
}
