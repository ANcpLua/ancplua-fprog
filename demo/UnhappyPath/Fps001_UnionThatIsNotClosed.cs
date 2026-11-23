namespace Demo.Fps001_UnionThatIsNotClosed;

// Bug: neither union is closed, so the compiler is right that the switches below can throw
// SwitchExpressionException. Its warning CS8509 must stay visible.
[Fprog.Union]
public record Shape // expect: FP0401, FP0402
{
    public sealed record Circle(double Radius) : Shape;

    public sealed record Square(double Side) : Shape;
}

// The implicit constructor is protected: a type in any other project may derive from Payment.
[Fprog.Union]
public abstract record Payment // expect: FP0402
{
    public sealed record Card(string Number) : Payment;

    public sealed record Cash(decimal Amount) : Payment;
}

[Fprog.FunctionalCore]
public static class Logic
{
    public static double Area(Shape shape) => shape switch // expect: CS8509
    {
        Shape.Circle circle => circle.Radius * circle.Radius * 3,
        Shape.Square square => square.Side * square.Side,
    };

    public static decimal Fee(Payment payment) => payment switch // expect: CS8509
    {
        Payment.Card => 0.3m,
        Payment.Cash => 0m,
    };

    public static double Throws() => Area(new Shape());
}
