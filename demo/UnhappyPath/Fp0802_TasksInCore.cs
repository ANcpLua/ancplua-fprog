using System.Collections.Generic;
using System.Threading.Tasks;

namespace Demo.Fp0802_TasksInCore;

[Fprog.FunctionalCore]
public static class Loader
{
    public static int Blocking() => Task.Run(() => 42).Result; // expect: FP0802
    public static int Sum(Task<int> pending) // expect: FP0802
    {
        var copy = pending;
        return copy.Result;
    }
    public static int Load(Task<int> pending) => pending.GetAwaiter().GetResult(); // expect: FP0802

    public static async IAsyncEnumerable<int> Stream(IAsyncEnumerable<int> source) // expect: FP0802, FP0802
    {
        await foreach (var item in source)
            yield return item * 2;
    }
}
