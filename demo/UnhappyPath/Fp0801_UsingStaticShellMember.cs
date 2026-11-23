using static System.Console;
using static System.IO.File;

namespace Demo.Fp0801_UsingStaticShellMember;

// Bug: console output and file access inside the core. No type name is written, so nothing names the shell.
[Fprog.FunctionalCore]
public static class Report
{
    public static decimal Net(decimal gross)
    {
        WriteLine(gross); // expect: FP0801
        return gross / 1.2m;
    }

    public static int Length(string path) => ReadAllText(path).Length; // expect: FP0801

    public static bool Known(string path) => Exists(path); // expect: FP0801
}
