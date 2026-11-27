using System;

namespace Demo.Fp0701_ExtensionReceiver;

[Fprog.FunctionalCore]
public static class Counter
{
    public static (Func<int> Read, int Next) SnapshotAndAdvance(int count) => (() => count, count + 1);
}
