using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0802_TasksInCore;

[Fprog.FunctionalCore]
public static class Loader
{
    public static int Sum(int value) => value;
    public static ImmutableArray<int> Stream(ImmutableArray<int> source) => [.. source.Select(item => item * 2)];
}
