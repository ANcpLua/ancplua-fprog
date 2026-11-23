using System.Collections.Immutable;

namespace Demo.Fp0801_ShellValueWithoutTypeName;

[Fprog.FunctionalCore]
public static class Report
{
    public sealed record Snapshot(int First, ImmutableArray<int> Entries, int Status);

    public static int Read(Snapshot cursor)
    {
        var total = cursor.First + 1;
        foreach (var entry in cursor.Entries)
            total += entry;

        return total + cursor.Status;
    }

    public static Snapshot Pass(Snapshot snapshot) => snapshot;
}
