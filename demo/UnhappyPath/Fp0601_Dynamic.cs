namespace Demo.Fp0601_Dynamic;

[Fprog.FunctionalCore]
public static class Pricing
{
    public static decimal Total(dynamic item) => item.Price * item.Quantity; // expect: FP0601
}
