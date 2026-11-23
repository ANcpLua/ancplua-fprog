using System;

namespace Demo.Fp0104_SpanOverParameter;

[Fprog.FunctionalCore]
public static class Markers
{
    public static int[] Reset(int[] values)
    {
        int[] updated = [.. values];
        var span = updated.AsSpan();
        span.Fill(0);
        if (span.Length > 0)
            span[0] = 1;
        return updated;
    }
}
