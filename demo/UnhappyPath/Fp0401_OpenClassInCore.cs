namespace Demo.Fp0401_OpenClassInCore;

[Fprog.FunctionalCore]
public abstract record Shape // expect: FP0401
{
    private Shape() { }

    public sealed record Circle(double Radius) : Shape;

    public sealed record Square(double Side) : Shape;

    public static double Area(Shape shape) => shape switch // expect: CS8509
    {
        Circle circle => circle.Radius * circle.Radius * 3,
        Square square => square.Side * square.Side,
    };
}
