using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Demo.KnownGaps.Fp0802_BlockingOnTask;

// Not reported today: FP0802 looks at the return type of methods only.
[Fprog.FunctionalCore]
public static class Loader
{
    public static int Sum(Task<int> pending) => pending.Result;

    public static int Load(Func<Task<int>> load) => load().GetAwaiter().GetResult();

    public static async IAsyncEnumerable<int> Stream(IAsyncEnumerable<int> source)
    {
        await foreach (var item in source)
            yield return item;
    }
}
