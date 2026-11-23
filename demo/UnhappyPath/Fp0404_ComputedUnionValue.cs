using System.Runtime.CompilerServices;

namespace Demo.Fp0404_ComputedUnionValue;

[Fprog.FunctionalCore]
public static class Amounts
{
    [Union]
    public readonly struct Amount : IUnion
    {
        public Amount(int number) => Value = number;
        public Amount(string text) => Value = text;
        public object? Value { get; }
    }

    public static int ByDefaultLiteral() => Evaluate(default); // expect: FP0404
    public static int ByDefaultExpression() => Evaluate(default(Amount)); // expect: FP0404
    public static int ByConstructor() => Evaluate(new Amount()); // expect: FP0404
    public static int ByTargetTypedConstructor() => Evaluate(new()); // expect: FP0404

    public static int Evaluate(Amount amount) => amount switch
    {
        int number => number,
        string text => text.Length,
    };
}
