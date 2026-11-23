using System.Collections.Immutable;

namespace Demo.Fp0801_UnnamedShellExtension;

// Correct: the shell reads the file and hands the lines to the core.
[Fprog.FunctionalCore]
public static class Reports
{
    public static int TotalLength(ImmutableArray<string> lines)
    {
        var total = 0;
        foreach (var line in lines)
            total += line.Length;

        return total;
    }
}
