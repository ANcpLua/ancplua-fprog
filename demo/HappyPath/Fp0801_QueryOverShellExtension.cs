using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0801_QueryOverShellExtension;

// Correct: the shell reads the file and hands the lines to the core; the query runs over them.
[Fprog.FunctionalCore]
public static class Reports
{
    public static IEnumerable<int> LineLengths(ImmutableArray<string> lines) => from line in lines select line.Length;
}
