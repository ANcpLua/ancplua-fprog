using System;
using System.Collections.Generic;
using System.IO;

namespace Demo.Fp0801_QueryOverShellExtension;

[Fprog.FunctionalCore]
public sealed record ReportInput(string Path);

public static class ReportReading
{
    public static IEnumerable<T> Select<T>(this ReportInput input, Func<string, T> map)
    {
        foreach (var line in File.ReadLines(input.Path))
            yield return map(line);
    }
}

// The query binds to ReportReading.Select, so the lengths depend on the file's content. See FINDINGS.md.
[Fprog.FunctionalCore]
public static class Reports
{
    public static IEnumerable<int> LineLengths(ReportInput input) => from line in input select line.Length;
}
