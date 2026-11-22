using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0202Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0202_AutoPropertyWithSetInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Account
        {
            public int Balance { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_PrivateSetIsStillSet_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Account
        {
            public int Balance { get; private {|FP0202:set|}; }

            public static string Label { get; private {|FP0202:set|}; } = "";
        }
        """);

    [TestMethod]
    public Task FP0202_SetWithBodyAndFieldKeyword_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Counter
        {
            private readonly int[] _values = new int[1];

            public int Value
            {
                get => _values[0];
                {|FP0202:set|} => _values[0] = value;
            }

            public string Name
            {
                get;
                {|FP0202:set|} { field = value.Trim(); }
            } = "";
        }
        """);

    [TestMethod]
    public Task FP0202_SetOnlyProperty_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Sink
        {
            private readonly int[] _last = new int[1];

            public int Last
            {
                {|FP0202:set|} => _last[0] = value;
            }
        }
        """);

    [TestMethod]
    public Task FP0202_IndexerWithSetCountsAsProperty_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Grid
        {
            private readonly int[] _cells = new int[4];

            public int this[int index]
            {
                get => _cells[index];
                {|FP0202:set|} => _cells[index] = value;
            }
        }
        """);

    [TestMethod]
    public Task FP0202_AbstractAndVirtualAndOverrideSetWithoutBody_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract class Shape
        {
            public abstract int Width { get; {|FP0202:set|}; }

            public virtual int Height { get; {|FP0202:set|}; }
        }

        [Fprog.FunctionalCore]
        public sealed class Box : Shape
        {
            public override int Width { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_InterfacePropertyAndIndexerInCoreReportsAccessorWithoutBody_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Contracts
        {
            public interface IShape
            {
                int Width { get; {|FP0202:set|}; }

                int this[int index] { get; {|FP0202:set|}; }

                static abstract int Default { get; {|FP0202:set|}; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_ExplicitInterfaceImplementationInCore_Reports() => VerifyAsync("""
        public interface IHasWidth
        {
            int Width { get; set; }
        }

        [Fprog.FunctionalCore]
        public sealed class Shape : IHasWidth
        {
            int IHasWidth.Width { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_PartialPropertyReportsBothDeclarations_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Person
        {
            public partial string Name { get; {|FP0202:set|}; }

            public partial string Name
            {
                get => field;
                {|FP0202:set|} => field = value;
            }
        }
        """);

    [TestMethod]
    public Task FP0202_RecordAndStructPropertiesWithSet_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Person(string Name)
        {
            public int Age { get; {|FP0202:set|}; }
        }

        [Fprog.FunctionalCore]
        public struct Point
        {
            public int X { readonly get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_SetInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public sealed class Inner
            {
                public int Value { get; {|FP0202:set|}; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public sealed class Anywhere
        {
            public int Value { get; {|FP0202:set|}; }
        }

        public interface IAnywhere
        {
            int Value { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_SetOutsideCore_NoReport() => VerifyAsync("""
        public sealed class Shell
        {
            private readonly int[] _cells = new int[4];

            public int Value { get; set; }

            public string Name { get; private set; } = "";

            public int this[int index]
            {
                get => _cells[index];
                set => _cells[index] = value;
            }
        }

        public interface IShell
        {
            int Value { get; set; }
        }
        """);

    [TestMethod]
    public Task FP0202_InitAndGetOnlyPropertiesInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Person
        {
            public string Name { get; init; } = "";

            public int Age { get; private init; }

            public int Id { get; }

            public string Display => Name + " " + Age;

            public int Next
            {
                get => Id + 1;
            }

            public required string Email { get; init; }
        }

        [Fprog.FunctionalCore]
        public static class Contracts
        {
            public interface IPerson
            {
                string Name { get; init; }

                int Age { get; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_PositionalRecordStructSynthesizedSetter_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public record struct Point(int X, int Y);

        [Fprog.FunctionalCore]
        public sealed record Person(string Name, int Age);
        """);

    [TestMethod]
    public Task FP0202_EventAccessorsAndSetterMethodNamesInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Bus
        {
            public event System.EventHandler Changed
            {
                add { }
                remove { }
            }

            public static Bus SetValue(int value) => new() { Value = value };

            public int Value { get; init; }
        }
        """);

    [TestMethod]
    public Task FP0202_GeneratedCodeInCore_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public sealed class Generated
        {
            public int Value { get; set; }
        }
        """);

    [TestMethod]
    public Task FP0202_ExtensionBlockPropertiesInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class GridExtensions
        {
            extension(int[] cells)
            {
                public int First
                {
                    get => cells[0];
                    {|FP0202:set|} => cells[0] = value;
                }

                public int Size => cells.Length;
            }

            extension(string)
            {
                public static int Width
                {
                    get => 0;
                    {|FP0202:set|} { }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_ExtensionBlockOnCoreTypeDeclaredOutsideCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Cells
        {
            public int[] Values { get; } = new int[1];
        }

        public static class ShellExtensions
        {
            extension(Cells cells)
            {
                public int First
                {
                    get => cells.Values[0];
                    set => cells.Values[0] = value;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_GeneratedCodeAttributeOnCoreType_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
        public sealed class Generated
        {
            public int Value { get; set; }
        }
        """);

    [TestMethod]
    public Task FP0202_RequiredPropertyWithSet_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Order
        {
            public required string Id { get; {|FP0202:set|}; }

            public required int Count { get; init; }
        }
        """);

    [TestMethod]
    public Task FP0202_ReadonlyStructAndReadonlySetAccessor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly struct Meter
        {
            public int Value
            {
                get => 0;
                {|FP0202:set|} { }
            }
        }

        [Fprog.FunctionalCore]
        public struct Gauge
        {
            public int Raw
            {
                get => 0;
                readonly {|FP0202:set|} { }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_InterfaceDefaultAndStaticSetInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Contracts
        {
            public interface IScale
            {
                int Factor
                {
                    get => 1;
                    {|FP0202:set|} { }
                }

                static virtual int Unit
                {
                    get => 1;
                    {|FP0202:set|} { }
                }

                static int Shared { get; {|FP0202:set|}; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_PositionalRecordRedeclaresParameterPropertyWithSet_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Person(string Name)
        {
            public string Name { get; {|FP0202:set|}; } = Name;
        }

        [Fprog.FunctionalCore]
        public record struct Point(int X)
        {
            public int X { get; {|FP0202:set|}; } = X;

            public int Y { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_PartialTypeWithAttributeOnOtherPart_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Ledger
        {
        }

        public sealed partial class Ledger
        {
            public int Total { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_GenericAndFileLocalTypesInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Box<T> where T : struct
        {
            public T? Value { get; {|FP0202:set|}; }

            public sealed class Slot<TKey> where TKey : notnull
            {
                public System.Collections.Generic.KeyValuePair<TKey, T> Entry { get; {|FP0202:set|}; }
            }
        }

        [Fprog.FunctionalCore]
        file sealed class Hidden
        {
            internal string? Note { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_SpanIsSetKeywordOnlyDespiteAttributesModifiersAndTrivia_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Account
        {
            private readonly int[] _cells = new int[1];

            public int Balance
            {
                get => _cells[0];
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                /* before */ private
                    {|FP0202:set|} // after
                    => _cells[0] = value;
            }

            public int Limit { get; /* gap */
                {|FP0202:set|}
                ; }
        }
        """);

    [TestMethod]
    public Task FP0202_OnlyCoreSideOfInheritanceIsReported_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract class Shape
        {
            public abstract int Width { get; {|FP0202:set|}; }
        }

        public sealed class Square : Shape
        {
            public override int Width { get; set; }
        }

        public class Shell
        {
            public virtual int Height { get; set; }
        }

        [Fprog.FunctionalCore]
        public sealed class CoreShell : Shell
        {
            public override int Height
            {
                get => 0;
                {|FP0202:set|} { }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_CoreTypeNestedInShellReportsOnlyInsideCore_Reports() => VerifyAsync("""
        public sealed class Shell
        {
            public int Count { get; set; }

            [Fprog.FunctionalCore]
            public sealed class Core
            {
                public int Count { get; {|FP0202:set|}; }

                public sealed class Deeper
                {
                    public struct Deepest
                    {
                        public int Count { get; {|FP0202:set|}; }
                    }
                }
            }

            public sealed class Sibling
            {
                public int Count { get; set; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_UnsafePointerAndRefStructProperties_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed unsafe class Native
        {
            public int* Pointer { get; {|FP0202:set|}; }

            public delegate*<int, int> Callback { get; {|FP0202:set|}; }
        }

        [Fprog.FunctionalCore]
        public ref struct Window
        {
            public System.Span<int> Cells { get; {|FP0202:set|}; }
        }
        """);

    [TestMethod]
    public Task FP0202_ExplicitInterfaceIndexerImplementation_Reports() => VerifyAsync("""
        public interface IGrid
        {
            int this[int index] { get; set; }
        }

        [Fprog.FunctionalCore]
        public sealed class Grid : IGrid
        {
            int IGrid.this[int index]
            {
                get => index;
                {|FP0202:set|} { }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetBodyWithNestedLambdasAndLocalFunctionsReportsOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Deep
        {
            private readonly System.Func<int, int>[] _functions = new System.Func<int, int>[1];

            public int Value
            {
                get => _functions[0](0);
                {|FP0202:set|}
                {
                    System.Func<int, System.Func<int, System.Func<int, int>>> curry = a => b => c => a + b + c + value;
                    _functions[0] = x => Local(curry(x)(x)(x));

                    static int Local(int y) => y switch { > 0 => y, _ => -y };
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_LookalikeAttributeInOtherNamespace_NoReport() => VerifyAsync("""
        namespace Other
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class FunctionalCoreAttribute : System.Attribute { }

            [FunctionalCore]
            public sealed class NotCore
            {
                public int Value { get; set; }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_FunctionalCoreUsingShellSettersAndPatterns_NoReport() => VerifyAsync("""
        using System.Collections.Immutable;
        using System.Linq;

        public sealed class ShellOptions
        {
            public int Retries { get; set; }
        }

        [Fprog.FunctionalCore]
        public static class Pricing
        {
            public sealed record Line(string Sku, decimal Price, int Quantity)
            {
                public decimal Total => Price * Quantity;
            }

            public readonly record struct Tag(string Value);

            public static ShellOptions Defaults() => new() { Retries = 3 };

            public static Line Discount(Line line) => line with { Price = line.Price * 0.9m };

            public static bool IsBulk(Line line) => line is { Quantity: > 10 };

            public static decimal Sum(ImmutableArray<Line> lines)
            {
                var total = 0m;
                foreach (var line in lines)
                    total += line.Total;
                return total;
            }

            public static ImmutableArray<string> Skus(ImmutableArray<Line> lines)
                => [.. from line in lines where line.Quantity > 0 select line.Sku];

            public static string Describe(object value) => value switch
            {
                Line { Sku: var sku } => sku,
                _ => nameof(ShellOptions.Retries),
            };
        }
        """);

    [TestMethod]
    public Task FP0202_RefReturningPropertiesAndInitIndexer_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Cells
        {
            private readonly int[] _cells = new int[4];

            public ref int First => ref _cells[0];

            public ref readonly int Last => ref _cells[^1];

            public int this[int index] => _cells[index];

            public int this[string key]
            {
                get => key.Length;
                init { }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetInUnionBodyNestedInCoreClass_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pets
        {
            public record Cat;
            public record Dog;
            public union Pet(Cat, Dog)
            {
                public int Tag
                {
                    get => 0;
                    {|FP0202:set|} { }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetInTopLevelUnionUnderAssemblyCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public record Cat;
        public record Dog;
        public union Pet(Cat, Dog)
        {
            public int Tag
            {
                get => 0;
                {|FP0202:set|} { }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetInClassNestedInUnionBody_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pets
        {
            public record Cat;
            public record Dog;
            public union Pet(Cat, Dog)
            {
                public sealed class Box
                {
                    public int Tag { get; {|FP0202:set|}; }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetIndexerAndStaticSetInNamespacedUnionUnderAssemblyCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        namespace Zoo
        {
            public record Cat;
            public record Dog;
            public union Pet(Cat, Dog)
            {
                public static int Count { get; {|FP0202:set|}; }

                public int this[int index]
                {
                    get => index;
                    {|FP0202:set|} { }
                }

                public int Size
                {
                    get => 0;
                    init { }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_CoreClassNestedInShellUnionBody_Reports() => VerifyAsync("""
        public static class Shell
        {
            public record Cat;
            public record Dog;
            public union Pet(Cat, Dog)
            {
                public int Tag
                {
                    get => 0;
                    set { }
                }

                [Fprog.FunctionalCore]
                public sealed class Box
                {
                    public int Tag { get; {|FP0202:set|}; }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetInUnionBodyOutsideCore_NoReport() => VerifyAsync("""
        public record Cat;
        public record Dog;
        public union Pet(Cat, Dog)
        {
            public int Tag
            {
                get => 0;
                set { }
            }
        }

        public static class Shell
        {
            public union Pet(Cat, Dog)
            {
                public sealed class Box
                {
                    public int Tag { get; set; }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0202_SetOnAttributeUnionAndItsCasesInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public string Label { get; {|FP0202:set|}; } = "";

            public sealed record Circle(double Radius) : Shape
            {
                public double Radius { get; {|FP0202:set|}; } = Radius;
            }

            public sealed record Square(double Side) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0202_SetOnAttributeUnionOutsideCore_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public string Label { get; set; } = "";

            public sealed record Circle(double Radius) : Shape
            {
                public double Radius { get; set; } = Radius;
            }
        }
        """);

    [TestMethod]
    public Task FP0202_PartialPropertyAcrossTypePartsReportsEachSetKeyword_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Person
        {
            public partial string Name { get; {|FP0202:set|}; }
        }

        public sealed partial class Person
        {
            public partial string Name
            {
                get => field;
                {|FP0202:set|} => field = value.Trim();
            }

            public partial int Age { get; init; }

            public partial int Age { get => field; init => field = value; }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0202", source, testContext.CancellationToken);
}
