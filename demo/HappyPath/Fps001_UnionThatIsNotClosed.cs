namespace Demo.Fps001_UnionThatIsNotClosed;

// Correct: abstract, private constructor, sealed nested cases. No other value can exist, so the suppressor
// may hide CS8509 and the switch needs no catch-all.
[Fprog.Union]
public abstract record Shape
{
    private Shape() { }

    public sealed record Circle(double Radius) : Shape;

    public sealed record Square(double Side) : Shape;
}

[Fprog.FunctionalCore]
public static class Logic
{
    public static double Area(Shape shape) => shape switch
    {
        Shape.Circle circle => circle.Radius * circle.Radius * 3,
        Shape.Square square => square.Side * square.Side,
    };
}
