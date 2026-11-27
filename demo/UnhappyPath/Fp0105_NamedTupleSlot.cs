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
        items.Add(round.Bonus); // expect: FP0105
        return round.Items.Count;
    }
}
