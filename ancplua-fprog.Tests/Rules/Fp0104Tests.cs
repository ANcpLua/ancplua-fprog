using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0104Tests(TestContext testContext)
{
    private const string Types = """

        public class Box
        {
            public int Count;
            public string? Name { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public Box? Inner { get; set; }
            public Box[] Items = [];
            public System.Collections.Generic.List<int> Values { get; } = [];
            public event System.EventHandler? Changed;
            public Box Get() => this;
            public void Raise() => Changed?.Invoke(this, System.EventArgs.Empty);
        }

        public sealed class DerivedBox : Box;

        public struct Point
        {
            public int X;
            public int Y;
        }

        public interface IHasCount
        {
            int Count { get; set; }
        }
        """;

    [TestMethod]
    public Task FP0104_FieldAssignment_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Set(Box box)
            {
                {|FP0104:box.Count|} = 1;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_PropertyAssignment_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static Box Rename(Box box, string name)
            {
                {|FP0104:box.Name|} = name;
                return box;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_CompoundAssignment_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Add(Box box, int amount)
            {
                {|FP0104:box.Count|} += amount;
                {|FP0104:box.X|} *= 2;
                {|FP0104:box.Name|} += "!";
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_IncrementAndDecrement_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Bump(Box box)
            {
                {|FP0104:box.Count|}++;
                ++{|FP0104:box.X|};
                {|FP0104:box.Y|}--;
                return --{|FP0104:box.Count|};
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ArrayParameterElement_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Fill(int[] values, int[,] grid)
            {
                {|FP0104:values[0]|} = 1;
                {|FP0104:values[^1]|} += 2;
                {|FP0104:values[1]|}++;
                {|FP0104:grid[0, 1]|} = 3;
                return values[0];
            }
        }
        """);

    [TestMethod]
    public Task FP0104_IndexerOnParameter_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Put(List<int> list, Dictionary<string, int> map)
            {
                {|FP0104:list[0]|} = 1;
                {|FP0104:list[^1]|} = 2;
                {|FP0104:map["key"]|} = 3;
                {|FP0104:map["key"]|}++;
                return list.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ChainOfMembersAndElements_ReportsWholeTarget() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Deep(Box box)
            {
                {|FP0104:box.Inner!.Items[0].Inner!.Count|} = 1;
                {|FP0104:box.Values[0]|} = 2;
                {|FP0104:box.Items[0]|} = new Box();
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ChainThroughMethodCallCounts_Reports() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int ViaCall(Box box)
            {
                {|FP0104:box.Get().Count|} = 1;
                {|FP0104:box.Items.First().X|} = 2;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_CastOfParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Cast(Box box, object any)
            {
                {|FP0104:((DerivedBox)box).Count|} = 1;
                {|FP0104:((Box)any).X|} = 2;
                {|FP0104:(box as DerivedBox)!.Y|} = 3;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_CoalesceAssignment_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static string Default(Box box)
            {
                {|FP0104:box.Name|} ??= "none";
                return box.Name;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_DeconstructionAssignment_ReportsEachTarget() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Move(Box box, int[] values)
            {
                int local;
                ({|FP0104:box.X|}, {|FP0104:box.Y|}) = (1, 2);
                ({|FP0104:values[0]|}, (local, {|FP0104:box.Count|})) = (3, (4, 5));
                return local;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_NullConditionalAssignment_ReportsFromParameter() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static void Maybe(Box? box)
            {
                {|FP0104:box?.Name|} = "x";
                {|FP0104:box?.Inner?.Count|} = 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_EventSubscriptionOnParameterCounts_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static Box Watch(Box box, System.EventHandler handler)
            {
                {|FP0104:box.Changed|} += handler;
                {|FP0104:box.Changed|} -= handler;
                return box;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_InterfaceParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Reset(IHasCount counter)
            {
                {|FP0104:counter.Count|} = 0;
                return counter.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_UnconstrainedTypeParameterMayBeReference_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Reset<T>(T counter) where T : IHasCount
            {
                {|FP0104:counter.Count|} = 0;
                return counter.Count;
            }

            public static T[] First<T>(T[] values, T value)
            {
                {|FP0104:values[0]|} = value;
                return values;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ClassConstrainedTypeParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Reset<T>(T counter) where T : class, IHasCount
            {
                {|FP0104:counter.Count|} = 0;
                return counter.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_StructConstrainedTypeParameter_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static T Reset<T>(T counter) where T : struct, IHasCount
            {
                counter.Count = 0;
                counter.Count++;
                return counter;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_StructParameterByValue_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static Point Shift(Point point)
            {
                point.X = 1;
                point.Y += 2;
                point.X++;
                (point.X, point.Y) = (point.Y, point.X);
                return point;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_RefStructParameterWritesCallerStorage_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Shift(ref Point point, out Point other)
            {
                other = default;
                {|FP0104:point.X|} = 1;
                {|FP0104:other.Y|}++;
                return point.X;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_LambdaParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static System.Action<Box> Reset() => b => {|FP0104:b.Count|} = 0;

            public static int Local(Box box)
            {
                return Set(box);

                static int Set(Box target) => {|FP0104:target.Count|} = 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_PrimaryConstructorParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Counter(Box box)
        {
            public int Next() => ++{|FP0104:box.Count|};
        }
        """);

    [TestMethod]
    public Task FP0104_WriteInConstructor_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Owner
        {
            public Owner(Box box)
            {
                {|FP0104:box.Count|} = 1;
                Box = box;
            }

            public Box Box { get; }
        }
        """);

    [TestMethod]
    public Task FP0104_InTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static int Set(Box box) => {|FP0104:box.Count|} = 1;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static int Set(Box box) => {|FP0104:box.Count|} = 1;
        }
        """);

    [TestMethod]
    public Task FP0104_OutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static int Set(Box box, int[] values)
            {
                box.Count = 1;
                box.Name ??= "x";
                box.X++;
                values[0] = 2;
                (box.X, box.Y) = (1, 2);
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_AssigningParameterItself_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static Box Pick(Box box, Box other, int count, string? name)
            {
                box = other;
                count++;
                count += 2;
                name ??= "x";
                (box, other) = (other, box);
                return count > 0 && name.Length > 0 ? box : other;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ThisAndLocalsAndStatics_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Builder
        {
            private static int total;
            private int count;
            private readonly int[] cells = new int[4];

            public Builder(Box box)
            {
                count = box.Count;
                this.count += box.X;
            }

            public int Build(Box box)
            {
                var copy = new Box { Count = box.Count, Name = box.Name };
                copy.Count++;
                copy.Items = [new Box()];
                copy.Items[0].X = box.X;
                var local = new int[2];
                local[0] = box.Count;
                count++;
                cells[0] = box.Y;
                total += box.Count;
                return copy.Count + local[0] + count + total;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ReadsAndWithExpressionsOfParameter_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public sealed record Account(string Owner, decimal Balance);

            public static Account Deposit(Account account, decimal amount)
                => account with { Balance = account.Balance + amount };

            public static int Sum(Box box, int[] values) => box.Count + values[0] + box.Items.Length;

            public static Box Clone(Box box) => new() { Count = box.Count, X = box.X, Inner = { Count = 1 } };
        }
        """);

    [TestMethod]
    public Task FP0104_CompilerGeneratedRecordMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Models
        {
            public sealed record Money(decimal Amount, string Currency);

            public record Person(string Name, int Age);

            public sealed record Employee(string Name, int Age, string Role) : Person(Name, Age);

            public record struct Pair(int Left, int Right);
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0104", source + Types, testContext.CancellationToken);
}
