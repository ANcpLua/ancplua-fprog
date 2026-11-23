using System.Collections.Generic;
using System.IO;

namespace Demo.Fp0801_UnnamedShellExtension;

[Fprog.FunctionalCore]
public sealed record ReportInput(string Path);

public static class ReportReading
{
    public static IEnumerator<string> GetEnumerator(this ReportInput input) => File.ReadLines(input.Path).GetEnumerator();
}

// Bug: the loop reads the file through ReportReading.GetEnumerator, so the result depends on the file's content.
[Fprog.FunctionalCore]
public static class Reports
{
    public static int TotalLength(ReportInput input)
    {
        var total = 0;
        foreach (var line in input) // expect: FP0801
            total += line.Length;

        return total;
    }
}
