using System.Collections.Generic;

namespace Demo.Fp0105_NamedTupleSlot;

[Fprog.FunctionalCore]
public sealed class Scores
{
    private readonly List<int> _scores = [];

    public int ScoreWithBonus(int bonus)
    {
        var round = (Items: _scores, Bonus: bonus);
        ref var items = ref round.Items;
        items = new List<int>(_scores);
        round.Item1.Add(round.Bonus);
        return round.Items.Count;
    }
}
