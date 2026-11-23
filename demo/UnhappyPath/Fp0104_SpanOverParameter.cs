using System;

namespace Demo.Fp0104_SpanOverParameter;

[Fprog.FunctionalCore]
public static class Markers
{
    public static int[] Reset(int[] values)
    {
        var span = values.AsSpan();
        span.Fill(0); // expect: FP0104
        if (span.Length > 0)
            span[0] = 1; // expect: FP0104
        return values;
    }
}
