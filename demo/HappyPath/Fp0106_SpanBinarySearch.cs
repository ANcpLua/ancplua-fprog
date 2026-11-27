using System;

namespace Demo.Fp0106_SpanBinarySearch;

[Fprog.FunctionalCore]
public static class Directory
{
    private readonly struct OrdinalNeedle(string key) : IComparable<string>
    {
        public int CompareTo(string? other) => string.CompareOrdinal(key, other);
    }

    public static int Find(string[] names, string key)
        => names.AsSpan().BinarySearch(new OrdinalNeedle(key));
}
