using System;

namespace Demo.Fp0106_SpanBinarySearch;

[Fprog.FunctionalCore]
public static class Directory
{
    public static int Find(string[] names, string key)
        => names.AsSpan().BinarySearch(key); // expect: FP0106
}
