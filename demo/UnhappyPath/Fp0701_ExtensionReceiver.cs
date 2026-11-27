using System;
using Demo.Trusted;

namespace Demo.Trusted
{
    public static class CounterExtensions
    {
        extension(ref int count)
        {
            public int Advance() => ++count;
        }
    }
}

namespace Demo.Fp0701_ExtensionReceiver
{
    [Fprog.FunctionalCore]
    public static class Counter
    {
        public static (Func<int> Read, int Next) SnapshotAndAdvance(int count)
        {
            Func<int> read = () => count; // expect: FP0701
            return (read, count.Advance());
        }
    }
}
