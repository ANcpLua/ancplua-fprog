using System.Collections.Generic;

namespace Demo.Fp0501_IncompleteClosedHierarchy;

[Fprog.FunctionalCore]
public static class Scores
{
    public interface IMarked;
    public closed class Root<T>;
    public sealed class Marked<T> : Root<T>, IMarked;
    public sealed class Extra<T> : Root<List<T>>;

    public static int Score<T>(Root<T> value) => value switch
    {
        IMarked => 1,
        _ => 0,
    };

    public static int ScoreList(Root<List<int>> value) => value switch
    {
        Marked<List<int>> => 1,
        Extra<int> => 0,
    };
}
