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

    [TestMethod]
    public Task FP0104_ParenthesizedOrCommentedTarget_ReportsWholeLeftOperand() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Set(Box box)
            {
                {|FP0104:(box.Count)|} = 1;
                {|FP0104:((box).X)|}++;
                --{|FP0104:(box.Y)|};
                {|FP0104:box /* why */
                    .Inner!
                    .Count|} += 2;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_NullConditionalElementAndCompound_Reports() => VerifyAsync("""
        using System.Collections.Generic;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Maybe(Box? box, int[]? values, List<int>? list)
            {
                {|FP0104:values?[0]|} = 1;
                {|FP0104:list?[0]|} += 2;
                {|FP0104:box?.Items[0].Count|} = 3;
                {|FP0104:box?.Inner?.Name|} ??= "x";
                {|FP0104:box?.Count|} -= 4;
                return 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_RefReturningMemberTarget_Reports() => VerifyAsync("""
        public sealed class Slots
        {
            private int first;
            private readonly int[] cells = new int[2];
            public ref int First => ref first;
            public ref int At(int index) => ref cells[index];
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Set(Slots slots)
            {
                {|FP0104:slots.First|} = 1;
                {|FP0104:slots.At(0)|} = 2;
                {|FP0104:slots.At(1)|}++;
                return slots.First;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_SpanAndRefFieldStructParametersByValue_NoReport() => VerifyAsync("""
        public ref struct Holder
        {
            public ref int Ref;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Fill(System.Span<int> span, System.Memory<int> memory, Holder holder, params System.Span<int> rest)
            {
                span[0] = 1;
                span[^1] += 1;
                memory.Span[0]++;
                holder.Ref = 5;
                rest[0] = 6;
                return span[0];
            }
        }
        """);

    [TestMethod]
    public Task FP0104_StructParameterHoldingReference_NoReport() => VerifyAsync("""
        public struct Wrapper
        {
            public Box Box;
            public int[] Values;
            public Point Point;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static Wrapper Touch(Wrapper wrapper)
            {
                wrapper.Box.Count = 1;
                wrapper.Values[0]++;
                wrapper.Box.Items[0] = new Box();
                wrapper.Point.X = 2;
                wrapper.Box = new Box();
                return wrapper;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_RefReassignmentAndBoxedCopy_NoReport() => VerifyAsync("""
        public ref struct Holder
        {
            public ref int Ref;
        }

        public struct Counter : IHasCount
        {
            public int Count { get; set; }
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Rebind(Holder holder, int[] values)
            {
                holder.Ref = ref values[0];
                return holder.Ref;
            }

            public static int Boxed<T>(Counter counter, T value) where T : struct, IHasCount
            {
                ((IHasCount)counter).Count = 0;
                ((IHasCount)value).Count = 1;
                return counter.Count + value.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_SpanLambdaParameterOfStringCreate_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Text
        {
            public static string Reverse(string text)
                => string.Create(text.Length, text, static (span, source) =>
                {
                    for (var i = 0; i < span.Length; i++)
                        span[i] = source[source.Length - 1 - i];
                });
        }
        """);

    [TestMethod]
    public Task FP0104_SpanParameterOfHelperFillingLocalBuffer_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Hex
        {
            public static string Format(byte value)
            {
                System.Span<char> buffer = stackalloc char[2];
                var written = Write(buffer, value);
                return new string(buffer[..written]);
            }

            private static int Write(System.Span<char> destination, byte value)
            {
                destination[0] = Digit(value >> 4);
                destination[1] = Digit(value & 0xF);
                return 2;
            }

            private static char Digit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
        }
        """);

    [TestMethod]
    public Task FP0104_TupleAccumulatorHoldingArray_NoReport() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Histogram
        {
            public static int[] Count(int[] values, int buckets)
                => values.Aggregate(
                    (Counts: new int[buckets], Total: 0),
                    (acc, value) =>
                    {
                        acc.Counts[value % buckets]++;
                        return (acc.Counts, acc.Total + 1);
                    }).Counts;
        }
        """);

    [TestMethod]
    public Task FP0104_SpanReachedFromReferenceTypeParameter_Reports() => VerifyAsync("""
        using System;

        public sealed class Buffer
        {
            public Memory<int> Memory;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Fill(int[] values, Buffer buffer)
            {
                {|FP0104:values.AsSpan()[0]|} = 1;
                {|FP0104:values.AsSpan(1)[^1]|} += 2;
                {|FP0104:buffer.Memory.Span[0]|}++;
                return values[0];
            }
        }
        """);

    [TestMethod]
    public Task FP0104_HelperFillingArrayParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Hex
        {
            private static int Write(char[] destination, byte value)
            {
                {|FP0104:destination[0]|} = Digit(value >> 4);
                {|FP0104:destination[1]|} = Digit(value & 0xF);
                return 2;
            }

            private static char Digit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
        }
        """);

    [TestMethod]
    public Task FP0104_ArrayAccumulatorLambdaParameter_Reports() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Histogram
        {
            public static int[] Count(int[] values, int buckets)
                => values.Aggregate(
                    new int[buckets],
                    (acc, value) =>
                    {
                        {|FP0104:acc[value % buckets]|}++;
                        return acc;
                    });
        }
        """);

    [TestMethod]
    public Task FP0104_RefParameterOfStructHoldingReferenceOrSpan_Reports() => VerifyAsync("""
        public struct Wrapper
        {
            public Box Box;
            public int[] Values;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Touch(ref Wrapper wrapper, ref System.Span<int> span)
            {
                {|FP0104:wrapper.Box.Count|} = 1;
                {|FP0104:wrapper.Values[0]|}++;
                {|FP0104:span[0]|} = 2;
                return span[0];
            }
        }
        """);

    [TestMethod]
    public Task FP0104_SpanExtensionReceiverAndInSpanParameter_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class SpanOps
        {
            public static int FillWith(this System.Span<int> span, int value)
            {
                for (var i = 0; i < span.Length; i++)
                    span[i] = value;
                return span.Length;
            }

            public static int First(in System.Span<int> span) => span[0] = 1;
        }
        """);

    [TestMethod]
    public Task FP0104_AccessorIndexerAndOperatorParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Holder
        {
            private readonly Box box = new();

            public Box Current
            {
                get => box;
                init
                {
                    {|FP0104:value.Count|} = 0;
                    box = value;
                }
            }

            public int this[Box key] => {|FP0104:key.X|} = 1;

            public static Holder operator +(Holder holder, Box extra)
            {
                {|FP0104:extra.Y|}++;
                return holder;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_InitializerWritesPrimaryConstructorParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Seeded(Box box)
        {
            private readonly int first = {|FP0104:box.Count|}++;
            public int Second { get; } = {|FP0104:box.X|} = 2;
            public int Sum => first + Second;
        }

        [Fprog.FunctionalCore]
        public sealed record Tagged(Box Source)
        {
            public int Tag { get; } = {|FP0104:Source.Count|} += 1;
        }
        """);

    [TestMethod]
    public Task FP0104_ExtensionReceiverParameters_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class BoxExtensions
        {
            public static int Reset(this Box box) => {|FP0104:box.Count|} = 0;

            extension(Box target)
            {
                public int Clear() => {|FP0104:target.X|} = 0;

                public Box Self => target;

                public Box Me() => target;
            }

            public static int ViaExtensions(Box box)
            {
                {|FP0104:box.Self.Y|} = 1;
                {|FP0104:box.Me().Count|} = 2;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_IteratorAndAsyncBodies_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Threading.Tasks;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static IEnumerable<int> Walk(Box box)
            {
                {|FP0104:box.Count|}++;
                yield return box.Count;
            }

            public static async Task<int> Later(Box box)
            {
                await Task.Yield();
                {|FP0104:box.X|} = 1;
                return box.X;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_InsideInterpolationLambdaQueryAndSwitch_Reports() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static string Mixed(Box box, int[] values)
            {
                var text = $"{{|FP0104:box.Count|}++}";
                var picked = values.Select(v => {|FP0104:box.X|} += v).ToArray();
                var query = from v in values
                            let w = {|FP0104:box.Y|}++
                            select v + w;
                var kind = box.Count switch
                {
                    0 => {|FP0104:box.Name|} = "zero",
                    _ => "many"
                };
                return text + picked.Length + query.Sum() + kind;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_StructGenericInterfaceAndPartialShapes_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly struct Calc
        {
            public static int Set(Box box) => {|FP0104:box.Count|} = 1;
        }

        [Fprog.FunctionalCore]
        public static partial class Shapes<TItem> where TItem : class
        {
            public interface IVisitor
            {
                static int Visit(Box box) => {|FP0104:box.X|} = 2;

                int Default(Box box) => {|FP0104:box.Y|} = 3;
            }

            public static TItem Store(TItem[] items, TItem item) => {|FP0104:items[0]|} = item;

            public static partial int Split(Box box);

            public static partial int Split(Box box) => {|FP0104:box.Count|} = 4;
        }
        """);

    [TestMethod]
    public Task FP0104_DynamicParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static object Poke(dynamic target)
            {
                {|FP0104:target.Count|} = 1;
                {|FP0104:target[0]|} += 2;
                {|FP0104:target.Inner.X|}++;
                return target;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_ParameterReassignedThenWritten_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Fresh(Box box)
            {
                box = new Box();
                {|FP0104:box.Count|} = 1;
                return box.Count;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_GeneratedCode_NoReport() => FpAnalyzerTestHost.VerifyRuleAsync("FP0104", """
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static int Set(Box box) => box.Count = 1;
        }
        """ + Types, testContext.CancellationToken);

    [TestMethod]
    public Task FP0104_PointerParametersAndBodilessMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract unsafe class Native
        {
            public abstract int Abstract(Box box);

            [System.Runtime.InteropServices.DllImport("native")]
            public static extern int External(Box box);

            public static int Empty(Box box) { return 0; }

            public static int Pointers(int* cells, Point* point, delegate*<Box, int> callback)
            {
                *cells = 1;
                cells[1] = 2;
                point->X = 3;
                (*point).Y = 4;
                return cells[0] + callback(new Box());
            }
        }
        """);

    [TestMethod]
    public Task FP0104_PureCoreWithMutatingCallsAndLocals_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        using System.Text;

        [Fprog.FunctionalCore]
        public static class Pricing
        {
            public sealed record Line(string Sku, int Quantity, decimal Price);

            public sealed record Order(ImmutableList<Line> Lines, decimal Discount);

            public static decimal Total(Order order)
            {
                var total = 0m;
                foreach (var line in order.Lines)
                    total += line.Quantity * line.Price;
                var builder = new StringBuilder();
                builder.Append(order.Discount);
                var lines = ImmutableArray.CreateBuilder<Line>();
                lines.Add(order.Lines[0]);
                var point = new Point();
                point.X += lines.Count;
                return total - order.Discount + point.X;
            }

            public static Order AddLine(Order order, Line line)
                => order with { Lines = order.Lines.Add(line) };

            public static string Describe(Line line) => line switch
            {
                { Quantity: 0 } => "empty",
                { Price: > 100m } expensive => expensive.Sku,
                _ => line.Sku
            };

            public static int CallsAreNotAssignments(List<int> list, Box box)
            {
                list.Add(1);
                list.Sort();
                box.Raise();
                return list.Sum();
            }
        }
        """);

    [TestMethod]
    public Task FP0104_InAndRefReadonlyParameters_Reports() => VerifyAsync("""
        public struct Wrapper
        {
            public Box Box;
            public Point Point;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Touch(in Box box, ref readonly Wrapper wrapper, in Point point)
            {
                {|FP0104:box.Count|} = 1;
                wrapper.Box.X += 2;
                return point.X + wrapper.Point.Y;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_MutatedLocalCopiesOfParameterData_NoReport() => VerifyAsync("""
        using System;
        using System.Collections.Generic;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int[] Sorted(int[] values, IEnumerable<int> source, Point point)
            {
                var copy = values.ToArray();
                copy[0] = 1;
                var list = new List<int>(source);
                list[0] = 2;
                var shifted = point;
                shifted.X++;
                var span = copy.AsSpan();
                span[1] = shifted.X + list[0];
                return copy;
            }
        }
        """);

    [TestMethod]
    public Task FP0104_RefAndOutArgumentsAreNotAssignments_NoReport() => VerifyAsync("""
        public static class Io
        {
            public static bool TryRead(string text, out int value) => int.TryParse(text, out value);

            public static int Bump(ref int value) => ++value;
        }

        [Fprog.FunctionalCore]
        public static class Ops
        {
            public static int Read(Box box, int[] values)
            {
                var ok = Io.TryRead("1", out box.Count);
                return Io.Bump(ref values[0]) + (ok ? box.Count : 0);
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0104", source + Types, testContext.CancellationToken);
}
