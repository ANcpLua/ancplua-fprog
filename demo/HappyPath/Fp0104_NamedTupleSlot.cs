using System.Collections.Generic;

namespace Demo.Fp0104_NamedTupleSlot;

[Fprog.FunctionalCore]
public static class Scores
{
    public static int ScoreWithBonus(List<int> scores, int bonus)
    {
        var round = (Items: scores, Bonus: bonus);
        round.Item1 = new List<int>(scores);
        round.Items.Add(round.Bonus);
        return round.Items.Count;
    }
}
