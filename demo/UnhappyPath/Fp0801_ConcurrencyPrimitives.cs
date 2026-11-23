using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Demo.Fp0801_ConcurrencyPrimitives;

[Fprog.FunctionalCore]
public static class Compute
{
    private static readonly ReaderWriterLockSlim Gate = new(); // expect: FP0801
    private static readonly AsyncLocal<int> Ambient = new(); // expect: FP0801

    public static ImmutableArray<int> Unordered(ImmutableArray<int> values)
        => [.. values.AsParallel().Select(value => value * 2)]; // expect: FP0801

    public static int Guarded()
    {
        Gate.EnterReadLock();
        try { return Ambient.Value; }
        finally { Gate.ExitReadLock(); }
    }

    public static bool Queue() => ThreadPool.QueueUserWorkItem(new WaitCallback(static _ => { })); // expect: FP0801, FP0801
}
