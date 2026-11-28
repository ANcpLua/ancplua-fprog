using System.Collections.Immutable;

namespace Demo.Fp0302_DefaultImmutableArray;

[Fprog.FunctionalCore]
public sealed record Order(string Id, ImmutableArray<decimal> Lines);

[Fprog.FunctionalCore]
public static class Orders
{
    // B17: default(ImmutableArray<T>) wraps a null array.
    public static Order Empty() => new Order("none", default);

    // B16: NullReferenceException for the order above.
    public static int Count(Order order) => order.Lines.Length;

    // B16: the list pattern reads Length and throws the same.
    public static decimal First(Order order) => order.Lines is [var first, ..] ? first : 0m;
}
