using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0103Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0103_RefParameterOnMethodInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Increment(ref int {|FP0103:value|}) => ++value;
        }
        """);

    [TestMethod]
    public Task FP0103_OutParameterOnMethodInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Splitter
        {
            public static int Split(int value, out int {|FP0103:remainder|})
            {
                remainder = value % 2;
                return value / 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_HandWrittenTryParseOutParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Age(int Years)
        {
            public static bool TryParse(string text, out Age? {|FP0103:age|})
            {
                age = int.TryParse(text, out var years) ? new Age(years) : null;
                return age is not null;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_HandWrittenDeconstruct_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Point(int x, int y)
        {
            public int X { get; } = x;

            public int Y { get; } = y;

            public void Deconstruct(out int {|FP0103:x|}, out int {|FP0103:y|})
            {
                x = X;
                y = Y;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_HandWrittenDeconstructOnRecord_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Range(int Low, int High)
        {
            public void Deconstruct(out int {|FP0103:width|}) => width = High - Low;
        }
        """);

    [TestMethod]
    public Task FP0103_CompilerGeneratedRecordDeconstruct_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Circle(double Radius);

        [Fprog.FunctionalCore]
        public readonly record struct Size(int Width, int Height);

        [Fprog.FunctionalCore]
        public static class Areas
        {
            public static int Area(Size size)
            {
                var (width, height) = size;
                return width * height;
            }

            public static double Diameter(Circle circle)
            {
                circle.Deconstruct(out var radius);
                return radius * 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_RefAndOutOnConstructor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Holder
        {
            public Holder(ref int {|FP0103:seed|}, out bool {|FP0103:created|})
            {
                Value = seed;
                created = true;
            }

            public int Value { get; }
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterOnPrimaryConstructor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Seeded(ref int {|FP0103:seed|})
        {
            public int Value { get; } = seed;
        }
        """);

    [TestMethod]
    public Task FP0103_LocalFunctionParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Sums
        {
            public static int Sum(int a, int b)
            {
                Add(a, b, out var result);
                return result;

                static void Add(int left, int right, out int {|FP0103:sum|}) => sum = left + right;
            }

            public static int Twice(int value)
            {
                Double(ref value);
                return value;

                static void Double(ref int {|FP0103:number|}) => number *= 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_LambdaWithExplicitModifiers_Reports() => VerifyAsync("""
        public delegate int Bump(ref int value);

        public delegate bool Probe(string text, out int value);

        [Fprog.FunctionalCore]
        public static class Lambdas
        {
            public static Bump Typed() => (ref int {|FP0103:value|}) => ++value;

            public static Probe Untyped() => (text, out {|FP0103:value|}) => int.TryParse(text, out value);
        }
        """);

    [TestMethod]
    public Task FP0103_LambdaWithImplicitRefFromDelegateType_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lambdas
        {
            public static System.Func<int, int> Typed() => value => value + 1;
        }
        """);

    [TestMethod]
    public Task FP0103_DelegateDeclarationNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Callbacks
        {
            public delegate void Update(ref int {|FP0103:state|});

            public delegate bool TryGet(string key, out string {|FP0103:value|});
        }
        """);

    [TestMethod]
    public Task FP0103_DelegateDeclarationUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public delegate void Update(ref int {|FP0103:state|});
        """);

    [TestMethod]
    public Task FP0103_DelegateDeclarationOutsideCore_NoReport() => VerifyAsync("""
        public delegate void Update(ref int state);

        [Fprog.FunctionalCore]
        public static class Uses
        {
            public static int Identity(int value) => value;
        }
        """);

    [TestMethod]
    public Task FP0103_ExtensionMethodRefReceiver_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class IntExtensions
        {
            public static void Reset(this ref int {|FP0103:value|}) => value = 0;
        }
        """);

    [TestMethod]
    public Task FP0103_ExtensionBlockRefReceiver_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class IntExtensions
        {
            extension(ref int {|FP0103:value|})
            {
                public void Reset() => value = 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_PartialMethodReportsDefinitionAndImplementation_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static partial class Parts
        {
            public static partial bool Read(string text, out int {|FP0103:value|});

            public static partial bool Read(string text, out int {|FP0103:value|}) => int.TryParse(text, out value);
        }
        """);

    [TestMethod]
    public Task FP0103_AbstractInterfaceAndOverrideMembers_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Contracts
        {
            public interface IReader
            {
                bool Read(out int {|FP0103:value|});
            }

            public abstract class Base
            {
                public abstract void Fill(ref int {|FP0103:value|});
            }

            public sealed class Derived : Base
            {
                public override void Fill(ref int {|FP0103:value|}) => value = 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_ScopedRefParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Spans
        {
            public static int Peek(scoped ref System.Span<int> {|FP0103:span|}) => span[0];
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterInStructAndTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public readonly struct Inner
            {
                public static void Swap(ref int {|FP0103:a|}, ref int {|FP0103:b|}) => (a, b) = (b, a);
            }
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static void Clear(out int {|FP0103:value|}) => value = 0;
        }
        """);

    [TestMethod]
    public Task FP0103_RefAndOutOutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static int Increment(ref int value) => ++value;

            public static void Clear(out int value) => value = 0;

            public static void Run()
            {
                var number = 1;
                Local(ref number);

                static void Local(ref int x) => x++;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_InAndRefReadonlyParameters_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Readers
        {
            public static int First(in System.ValueTuple<int, int> pair) => pair.Item1;

            public static int Second(ref readonly System.ValueTuple<int, int> pair) => pair.Item2;

            public static int Local(int value)
            {
                return Read(in value);

                static int Read(in int x) => x;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_InParametersOnOperatorAndIndexer_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly record struct Money(decimal Amount)
        {
            public static Money operator +(in Money left, in Money right) => new(left.Amount + right.Amount);

            public decimal this[in int factor] => Amount * factor;
        }
        """);

    [TestMethod]
    public Task FP0103_OutArgumentAndRefReturnAndRefLocal_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Callers
        {
            public static int ParseOrZero(string text) => int.TryParse(text, out var value) ? value : 0;

            public static ref readonly int FirstOf(int[] values) => ref values[0];

            public static int Copy(int[] values)
            {
                ref readonly var first = ref values[0];
                return first;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_FunctionPointerRefParameterHasNoName_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static unsafe class Pointers
        {
            public static int Call(delegate*<in int, int> function, int value) => function(in value);
        }
        """);

    [TestMethod]
    public Task FP0103_ValueParametersAndParams_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Plain
        {
            public static int Sum(int first, params int[] rest) => first + rest.Length;

            public static System.Func<int, int, int> Adder() => (int a, int b) => a + b;
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0103", source, testContext.CancellationToken);
}
