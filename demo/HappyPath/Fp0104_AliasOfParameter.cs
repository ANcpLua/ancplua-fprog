using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Demo.Fp0104_AliasOfParameter;

public sealed class Line
{
    public int Quantity;
}

public readonly record struct Point(int X, int Y);

public struct Cursor
{
    public int Position;
}

// Correct: every object that is written was created inside the function, so no caller can see the writes.
[Fprog.FunctionalCore]
public static class Normalizer
{
    public static ImmutableArray<int> Sorted(ImmutableArray<int> values)
    {
        var copy = values.ToArray();          // a new array
        System.Array.Sort(copy);
        copy[0] = copy[0] + 0;
        return [.. copy];
    }

    public static int FreshObject(Line template)
    {
        var line = new Line { Quantity = template.Quantity };   // a new object
        line.Quantity++;
        return line.Quantity;
    }

    public static int Reassigned(Line template)
    {
        var line = template;
        line = new Line();                    // from here on it is another object
        line.Quantity = 5;
        return line.Quantity;
    }

    public static int StructCopy(Cursor cursor)
    {
        var moved = cursor;                   // a struct is copied
        moved.Position++;
        return moved.Position;
    }

    public static int CopiedList(IEnumerable<int> source)
    {
        var list = source.ToList();           // a new list
        list[0] = 1;
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
