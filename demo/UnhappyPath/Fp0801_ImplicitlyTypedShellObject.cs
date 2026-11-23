namespace Demo.Fp0801_ImplicitlyTypedShellObject;

public static class Shell
{
    public static System.IO.StreamReader Open(string path) => new(path);

    public static System.Net.Http.HttpClient Client { get; } = new();
}

// Bug: the core reads a file and calls the network. The types come from the shell and are never written here.
[Fprog.FunctionalCore]
public static class Importer
{
    public static string FirstLine(string path)
    {
        var reader = Shell.Open(path); // expect: FP0801
        return reader.ReadLine() ?? ""; // expect: FP0801
    }

    public static string Everything(string path) => Shell.Open(path).ReadToEnd(); // expect: FP0801

    public static string Download(string url) => Shell.Client.GetStringAsync(url).Result; // expect: FP0801, FP0802
}
