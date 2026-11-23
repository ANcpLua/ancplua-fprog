using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0104_AliasOfParameter;

[Fprog.FunctionalCore]
public sealed record Line(int Quantity);

// Correct: every object that is written was created inside the function, so no caller can see the writes.
[Fprog.FunctionalCore]
public static class Normalizer
{
    public static ImmutableArray<int> Sorted(ImmutableArray<int> values)
    {
        var copy = values.ToArray();          // a new array
        System.Array.Sort(copy);
        return [.. copy];
    }

    public static int FreshObject(List<int> template)
    {
        var line = new List<int>(template);
        line.Add(1);
        return line.Count;
    }

    public static int Reassigned(List<int> template)
    {
        var line = template;
        line = new List<int>();
        line.Add(5);
        return line.Count;
    }

    public static int StructCopy((int Position, int Column) cursor)
    {
        var moved = cursor;                   // a struct is copied
        moved.Position++;
        return moved.Position;
    }

    public static int CopiedList(IEnumerable<int> source)
    {
        var list = source.ToList();           // a new list
        if (list.Count > 0) list[0] = 1;
        return list.Count;
    }

    public static int ReadOnly(List<Line> lines)
    {
        var total = 0;
        foreach (var line in lines)
            total += line.Quantity;           // reads only
        return total;
    }
}
