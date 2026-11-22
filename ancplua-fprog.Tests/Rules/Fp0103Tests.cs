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

    [TestMethod]
    public Task FP0103_MethodInUnannotatedPartialPart_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static partial class Split
        {
        }

        public static partial class Split
        {
            public static void Clear(out int {|FP0103:value|}) => value = 0;
        }
        """);

    [TestMethod]
    public Task FP0103_CoreTypeNestedInShellType_Reports() => VerifyAsync("""
        public static class Shell
        {
            public static void Outside(ref int value) => value++;

            [Fprog.FunctionalCore]
            public static class Core
            {
                public static void Inside(ref int {|FP0103:value|}) => value++;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterInGeneratedCode_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static void Clear(out int value) => value = 0;
        }
        """);

    [TestMethod]
    public Task FP0103_LambdaAndAnonymousMethodInInitializers_Reports() => VerifyAsync("""
        public delegate int Bump(ref int value);

        [Fprog.FunctionalCore]
        public sealed class Bumps
        {
            private static readonly Bump Field = (ref int {|FP0103:a|}) => ++a;

            public static Bump Property { get; } = (ref int {|FP0103:b|}) => ++b;

            public Bump Instance => delegate (ref int {|FP0103:c|}) { return ++c; };

            public static Bump FromField() => Field;
        }
        """);

    [TestMethod]
    public Task FP0103_LambdaWithNaturalDelegateType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Natural
        {
            public static int Twice(int seed)
            {
                var bump = (ref int {|FP0103:x|}) => x *= 2;
                bump(ref seed);
                return seed;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_DeeplyNestedLambdaAndSwitchArm_Reports() => VerifyAsync("""
        public delegate int Bump(ref int value);

        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static System.Func<System.Func<Bump>> Make() => () => () => (ref int {|FP0103:v|}) => ++v;

            public static Bump Pick(bool twice) => twice switch
            {
                true => (ref int {|FP0103:t|}) => t *= 2,
                false => static (ref int {|FP0103:f|}) => ++f,
            };
        }
        """);

    [TestMethod]
    public Task FP0103_LambdaInConstructorInitializerArguments_Reports() => VerifyAsync("""
        public delegate int Bump(ref int value);

        public abstract class Base(Bump bump)
        {
            public Bump Bump { get; } = bump;
        }

        [Fprog.FunctionalCore]
        public static class Holders
        {
            public sealed class Primary() : Base((ref int {|FP0103:x|}) => ++x);

            public sealed class Classic : Base
            {
                public Classic() : base((ref int {|FP0103:y|}) => ++y)
                {
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0103_DiscardLambdaParameters_Reports() => VerifyAsync("""
        public delegate int Pick(ref int first, ref int second);

        [Fprog.FunctionalCore]
        public static class Discards
        {
            public static Pick Zero() => (ref int {|FP0103:_|}, ref int {|FP0103:_|}) => 0;
        }
        """);

    [TestMethod]
    public Task FP0103_SpanIsNameOnlyWithAttributesCommentsAndVerbatimName_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Names
        {
            public static void Fill(
                [System.Diagnostics.CodeAnalysis.NotNull] /* before */ out
                // between
                string? {|FP0103:@event|} /* after */)
                => @event = "";

            public static void Reset(ref this int {|FP0103:value|}) => value = 0;
        }
        """);

    [TestMethod]
    public Task FP0103_GenericRefAndOutParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Box<T>(T Value)
        {
            public bool TryMap<TResult>(System.Func<T, TResult?> map, out TResult? {|FP0103:result|})
                where TResult : class
            {
                result = map(Value);
                return result is not null;
            }

            public static void Swap(ref Box<T> {|FP0103:left|}, ref Box<T> {|FP0103:right|})
                => (left, right) = (right, left);
        }
        """);

    [TestMethod]
    public Task FP0103_ImplementingBclInterfaceOutParameter_Reports() => VerifyAsync("""
        #nullable enable
        using System;
        using System.Diagnostics.CodeAnalysis;

        [Fprog.FunctionalCore]
        public sealed record Age(int Years) : IParsable<Age>
        {
            public static Age Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));

            static bool IParsable<Age>.TryParse(
                [NotNullWhen(true)] string? s,
                IFormatProvider? provider,
                [MaybeNullWhen(false)] out Age {|FP0103:result|})
            {
                result = int.TryParse(s, provider, out var years) ? new Age(years) : null;
                return result is not null;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_StaticAbstractVirtualAndDefaultInterfaceMembers_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Contracts
        {
            public interface IParser<TSelf>
                where TSelf : IParser<TSelf>
            {
                static abstract bool TryParse(string text, out TSelf {|FP0103:value|});

                static virtual void Reset(ref TSelf {|FP0103:value|})
                {
                }

                bool Count(out int {|FP0103:count|})
                {
                    count = 0;
                    return true;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0103_ExternMethodAndExternLocalFunction_Reports() => VerifyAsync("""
        using System.Runtime.InteropServices;

        [Fprog.FunctionalCore]
        public static class Native
        {
            [DllImport("native")]
            public static extern int Read(out int {|FP0103:value|});

            public static int Call()
            {
                var x = 0;
                return Local(ref x);

                [DllImport("native")]
                static extern int Local(ref int {|FP0103:value|});
            }
        }
        """);

    [TestMethod]
    public Task FP0103_LocalFunctionsInIteratorAndAsyncMethods_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Flows
        {
            public static IEnumerable<int> Halves(int[] values)
            {
                foreach (var value in values)
                {
                    if (TryHalf(value, out var half))
                        yield return half;
                }

                static bool TryHalf(int value, out int {|FP0103:half|})
                {
                    half = value / 2;
                    return value % 2 == 0;
                }
            }

            public static async Task<int> LaterAsync(int seed)
            {
                await Task.Yield();
                return Bump(ref seed);

                static int Bump(ref int {|FP0103:x|}) => ++x;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_PointerTypedRefAndOutParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static unsafe class Pointers
        {
            public static void Advance(ref int* {|FP0103:pointer|}) => pointer++;

            public static void Clear(out byte* {|FP0103:pointer|}) => pointer = null;
        }
        """);

    [TestMethod]
    public Task FP0103_PartialConstructorReportsBothParts_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Holder
        {
            public partial Holder(ref int {|FP0103:seed|});

            public partial Holder(ref int {|FP0103:seed|}) => Value = seed;

            public int Value { get; }
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterOnStructPrimaryConstructor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly struct Seeded(ref int {|FP0103:seed|})
        {
            public int Value { get; } = seed;
        }
        """);

    [TestMethod]
    public Task FP0103_InAndRefReadonlyOnLambdasDelegatesAndRecords_NoReport() => VerifyAsync("""
        public delegate int Reader(in int value);

        [Fprog.FunctionalCore]
        public static class Readers
        {
            public delegate int Peek(ref readonly int value);

            public readonly record struct Pair(in int Left, int Right);

            public static Reader Read() => (in int value) => value;

            public static Peek Look() => (ref readonly int value) => value;

            public static int Length(scoped in System.ReadOnlySpan<int> span) => span.Length;

            public static int Extend(this in Pair pair) => pair.Left + pair.Right;
        }
        """);

    [TestMethod]
    public Task FP0103_FunctionalCoreWithOutArgumentsAndRefLocals_NoReport() => VerifyAsync("""
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Inventory
        {
            public sealed record Item(string Name, int Quantity);

            public static ImmutableArray<Item> Restock(ImmutableArray<Item> items, ImmutableDictionary<string, int> deliveries)
                => [.. items.Select(item => deliveries.TryGetValue(item.Name, out var extra)
                    ? item with { Quantity = item.Quantity + extra }
                    : item)];

            public static int Total(System.ReadOnlySpan<int> quantities)
            {
                var total = 0;
                foreach (ref readonly var quantity in quantities)
                    total += quantity;
                return total;
            }

            public static string Describe(Item item) => item switch
            {
                { Quantity: 0 } => $"{item.Name}: out of stock",
                var (name, quantity) => $"{name}: {quantity}",
            };

            public static int Sum(ImmutableArray<Item> items)
                => (from item in items where item.Quantity > 0 select item.Quantity).Sum();
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterOnRecordExplicitConstructor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Seeded(int Seed)
        {
            public Seeded(ref int {|FP0103:seed|}, bool consume)
                : this(seed)
            {
                if (consume)
                    seed = 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_UnnamedAndInExtensionReceivers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class IntExtensions
        {
            extension(int)
            {
                public static int Zero() => 0;
            }

            extension(in int value)
            {
                public int Twice() => value * 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_RefParameterInGeneratedCodeAttributedType_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Mixed
        {
            [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
            public static class Generated
            {
                public static void Clear(out int value) => value = 0;
            }

            public static class Written
            {
                public static void Clear(out int {|FP0103:value|}) => value = 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0103_TypelessLambdaRefAndOutParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Swapper
        {
            public delegate int Swap(ref int {|FP0103:left|}, out int {|FP0103:right|});

            public static readonly Swap Run = (ref {|FP0103:a|}, out {|FP0103:b|}) =>
            {
                b = a;
                return a;
            };
        }
        """);

    [TestMethod]
    public Task FP0103_MemberParameterInsideExtensionBlock_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class IntExtensions
        {
            extension(int source)
            {
                public int AddTo(ref int {|FP0103:target|}) => target + source;

                public static int Halve(int value, out int {|FP0103:rest|})
                {
                    rest = value % 2;
                    return value / 2;
                }

                public int Plus(in int other) => source + other;
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0103", source, testContext.CancellationToken);
}
