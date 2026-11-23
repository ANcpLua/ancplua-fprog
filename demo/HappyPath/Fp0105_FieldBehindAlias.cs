using System.Collections.Immutable;

namespace Demo.Fp0105_FieldBehindAlias;

[Fprog.FunctionalCore]
public sealed class Scores(ImmutableDictionary<int, int> entries, ImmutableDictionary<int, int> initial)
{
    public int Count => entries.Count;

    public Scores Remember(int player, int score)
        => new(entries.SetItem(player, score), initial.ContainsKey(player) ? initial : initial.Add(player, score));
}
