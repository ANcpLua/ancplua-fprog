namespace Demo.Edge
{
    public delegate int Read();

    public sealed class Cursor : System.IDisposable
    {
        public int this[int index] => index;
        public static int operator +(Cursor cursor, int n) => n;
        public System.Collections.Generic.List<int>.Enumerator GetEnumerator() => new System.Collections.Generic.List<int> { 1 }.GetEnumerator();
        public void Dispose() { }
    }
}

namespace Demo.Trusted
{
    public static class CursorSource
    {
        public static Edge.Cursor Open() => new();
        public static Edge.Read Callback() => () => 1;
    }
}

namespace Demo.Fp0801_ShellValueWithoutTypeName
{
    [Fprog.FunctionalCore]
    public static class Report
    {
        public static int Read()
        {
            var cursor = Trusted.CursorSource.Open();
            var callback = Trusted.CursorSource.Callback();
            int total = cursor[0] + (cursor + 1); // expect: FP0801, FP0801

            using (cursor) // expect: FP0801
                foreach (var entry in cursor) // expect: FP0801
                    total += entry;

            return total + callback(); // expect: FP0801
        }

        private static object Wrap(object value) => value;

        public static object Pass() => Wrap(Trusted.CursorSource.Open()); // expect: FP0801
    }
}
