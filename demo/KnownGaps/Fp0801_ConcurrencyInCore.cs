using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.KnownGaps.Fp0801_ConcurrencyInCore;

// Not reported today: none of these types is in the FP0801 list.
[Fprog.FunctionalCore]
public static class Compute
{
    private static readonly ReaderWriterLockSlim Gate = new();
    private static readonly AsyncLocal<int> Ambient = new();

    public static int Blocking() => Task.Run(() => 42).Result;

    public static ImmutableArray<int> Unordered(ImmutableArray<int> values)
        => [.. values.AsParallel().Select(value => value * 2)];              // order can differ from run to run

    public static int Guarded()
    {
        Gate.EnterReadLock();
        try { return Ambient.Value; }
        finally { Gate.ExitReadLock(); }
    }

    public static bool Queue() => ThreadPool.QueueUserWorkItem(static _ => { });
}
