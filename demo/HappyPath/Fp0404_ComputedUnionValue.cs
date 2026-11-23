using System.Runtime.CompilerServices;

namespace Demo.Fp0404_ComputedUnionValue;

[Fprog.FunctionalCore]
public static class Amounts
{
    [Union]
    public readonly struct Amount : IUnion
    {
        private readonly int _number;
        private readonly string? _text;

        public Amount(int number) => _number = number;
        public Amount(string text) => _text = text;
        public object Value => _text is null ? (object)_number : _text;
    }

    public static int ByDefaultLiteral() => Evaluate(default);
    public static int ByDefaultExpression() => Evaluate(default(Amount));
    public static int ByConstructor() => Evaluate(new Amount());
    public static int ByTargetTypedConstructor() => Evaluate(new());

    public static int Evaluate(Amount amount) => amount switch
    {
        int number => number,
        string text => text.Length,
    };
}
