namespace Demo.Fp0601_Dynamic;

[Fprog.FunctionalCore]
public static class Pricing
{
    public sealed record Item(decimal Price, int Quantity);

    public static decimal Total(Item item) => item.Price * item.Quantity;
}
