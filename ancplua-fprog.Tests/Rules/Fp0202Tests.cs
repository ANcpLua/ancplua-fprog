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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0202", source, testContext.CancellationToken);
}
