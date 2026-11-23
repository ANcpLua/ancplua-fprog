using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0801_ImplicitlyTypedShellObject;

// Correct: the shell reads the file and the network; the core gets lines and text as values.
[Fprog.FunctionalCore]
public static class Importer
{
    public static string FirstLine(ImmutableArray<string> lines)
    {
        var first = lines.FirstOrDefault();
        return first ?? "";
    }

    public static int Words(string text) => text.Split(' ').Length;
}
