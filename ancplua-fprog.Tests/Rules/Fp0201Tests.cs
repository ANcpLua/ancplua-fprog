using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0201Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0201_InstanceFieldInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Counter
        {
            private int {|FP0201:_count|};
            public string {|FP0201:Name|} = "counter";

            public int Next() => _count + Name.Length;
        }
        """);

    [TestMethod]
    public Task FP0201_EachOfSeveralDeclaratorsInOneDeclaration_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Point
        {
            public int {|FP0201:X|}, {|FP0201:Y|} = 1, {|FP0201:Z|};
        }
        """);

    [TestMethod]
    public Task FP0201_StaticField_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Cache
        {
            private static int {|FP0201:_hits|};
            public static string? {|FP0201:Last|} = null;

            public static int Hits() => _hits;
        }
        """);

    [TestMethod]
    public Task FP0201_StructAndRecordStructFields_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public struct Money
        {
            public decimal {|FP0201:Amount|};
            private static int {|FP0201:_created|};
        }

        [Fprog.FunctionalCore]
        public record struct Range(int Start)
        {
            public int {|FP0201:End|};
        }
        """);

    [TestMethod]
    public Task FP0201_FieldInRecordClass_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Person(string Name)
        {
            public int {|FP0201:Age|};
        }
        """);

    [TestMethod]
    public Task FP0201_VolatileField_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Flag
        {
            private volatile bool {|FP0201:_set|};

            public bool IsSet() => _set;
        }
        """);

    [TestMethod]
    public Task FP0201_FieldLikeEventHasMutableBackingField_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Button
        {
            public event System.Action? {|FP0201:Clicked|}, {|FP0201:Pressed|};
            public static event System.EventHandler? {|FP0201:Created|};
        }
        """);

    [TestMethod]
    public Task FP0201_RefFieldWithReadonlyReferentIsReassignable_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public ref struct Cursor
        {
            public ref readonly int {|FP0201:Current|};
            public ref int {|FP0201:Target|};
        }
        """);

    [TestMethod]
    public Task FP0201_FixedSizeBuffer_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public unsafe struct Packet
        {
            public fixed byte {|FP0201:Data|}[4];
        }
        """);

    [TestMethod]
    public Task FP0201_FieldInPartialTypeAnnotatedOnOtherPart_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Ledger;

        public sealed partial class Ledger
        {
            private int {|FP0201:_balance|};

            public int Balance() => _balance;
        }
        """);

    [TestMethod]
    public Task FP0201_FieldInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public sealed class Inner
            {
                public int {|FP0201:Value|};

                public struct Deeper
                {
                    public int {|FP0201:Depth|};
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0201_FieldUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public sealed class Anywhere
        {
            public int {|FP0201:Value|};
        }

        public interface IDefaults
        {
            static int {|FP0201:Seed|};
        }
        """);

    [TestMethod]
    public Task FP0201_MutableFieldsOutsideCore_NoReport() => VerifyAsync("""
        public sealed class Shell
        {
            private int _count;
            public static string? Last;
            private volatile bool _set;
            public event System.Action? Clicked;

            public int Count() => _count + (_set ? 1 : 0);
        }

        public struct Mutable
        {
            public int Value;
        }
        """);

    [TestMethod]
    public Task FP0201_ReadonlyAndConstFields_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Settings
        {
            public const int Limit = 10;
            private const string A = "a", B = "b";
            private readonly int _value;
            public static readonly string Name = A + B;
            private readonly int _x = 1, _y = 2;

            public Settings(int value) => _value = value;

            public int Sum() => _value + _x + _y + Limit;
        }

        [Fprog.FunctionalCore]
        public readonly struct Money(decimal amount)
        {
            public readonly decimal Amount = amount;
        }

        [Fprog.FunctionalCore]
        public ref struct Cursor(ref int target)
        {
            public readonly ref int Target = ref target;
            public readonly ref readonly int Current = ref target;
        }
        """);

    [TestMethod]
    public Task FP0201_CompilerGeneratedBackingFields_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Circle(double Radius)
        {
            public string Label { get; init; } = "";
            public int Rank { get; } = 1;
            public int Kept { get => field; init => field = value * 2; }
        }

        [Fprog.FunctionalCore]
        public readonly record struct Point(int X, int Y);

        [Fprog.FunctionalCore]
        public record struct Size(int Width, int Height);

        [Fprog.FunctionalCore]
        public sealed class Area(int width)
        {
            public int Width => width;
        }
        """);

    [TestMethod]
    public Task FP0201_EnumMembersAreConst_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Colors
        {
            public enum Color { Red, Green = 2, Blue }

            public static Color Default() => Color.Red;
        }
        """);

    [TestMethod]
    public Task FP0201_EventsWithoutBackingField_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract class Source
        {
            public abstract event System.Action? Changed;

            public event System.Action? Explicit
            {
                add { }
                remove { }
            }

            public interface ISink
            {
                event System.Action? Received;
            }
        }

        [Fprog.FunctionalCore]
        public sealed partial class Feed
        {
            public partial event System.Action? Updated;

            public partial event System.Action? Updated
            {
                add { }
                remove { }
            }
        }
        """);

    [TestMethod]
    public Task FP0201_LocalVariablesInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Sums
        {
            public static int Total(int[] values)
            {
                var total = 0;
                int a = 1, b = 2;
                foreach (var value in values)
                    total += value;

                return total + a + b;
            }
        }
        """);

    [TestMethod]
    public Task FP0201_GeneratedCodeInCore_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public sealed class Generated
        {
            public int Value;
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0201", source, testContext.CancellationToken);
}
