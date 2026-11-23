using System.Collections.Generic;

namespace Demo.Fp0105_FieldBehindAlias;

[Fprog.FunctionalCore]
public sealed class Scores
{
    private readonly Dictionary<int, int> _scores = new();

    private IDictionary<int, int> Initial { get; set; } = new Dictionary<int, int>(); // expect: FP0202

    public int Count => _scores.Count;

    public Scores Remember(int player, int score)
    {
        var entries = _scores;
        entries[player] = score; // expect: FP0105
        Initial.TryAdd(player, score); // expect: FP0105
        return this;
    }
}
