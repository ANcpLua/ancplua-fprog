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
    public Task FP0104_FieldAssignment_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_PropertyAssignment_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_CompoundAssignment_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_IncrementAndDecrement_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ArrayParameterElement_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_IndexerOnParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ChainOfMembersAndElements_ReportsWholeTarget()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ChainThroughMethodCallCounts_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_CastOfParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_CoalesceAssignment_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_DeconstructionAssignment_ReportsEachTarget()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_NullConditionalAssignment_ReportsFromParameter()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_EventSubscriptionOnParameterCounts_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_InterfaceParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_UnconstrainedTypeParameterMayBeReference_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ClassConstrainedTypeParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_StructConstrainedTypeParameter_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_StructParameterByValue_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_RefStructParameterWritesCallerStorage_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_LambdaParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_PrimaryConstructorParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Counter(Box box)
                           {
                               public int Next() => ++{|FP0104:box.Count|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteInConstructor_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_InTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static int Set(Box box) => {|FP0104:box.Count|} = 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_UnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static int Set(Box box) => {|FP0104:box.Count|} = 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_OutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_AssigningParameterItself_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ThisAndLocalsAndStatics_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ReadsAndWithExpressionsOfParameter_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_CompilerGeneratedRecordMembers_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Models
                           {
                               public sealed record Money(decimal Amount, string Currency);

                               public record Person(string Name, int Age);

                               public sealed record Employee(string Name, int Age, string Role) : Person(Name, Age);

                               public record struct Pair(int Left, int Right);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ParenthesizedOrCommentedTarget_ReportsWholeLeftOperand()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_NullConditionalElementAndCompound_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_RefReturningMemberTarget_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_SpanAndRefFieldStructParametersByValue_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_StructParameterHoldingReference_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_RefReassignmentAndBoxedCopy_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_SpanLambdaParameterOfStringCreate_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_SpanParameterOfHelperFillingLocalBuffer_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_TupleAccumulatorHoldingArray_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_SpanReachedFromReferenceTypeParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_HelperFillingArrayParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ArrayAccumulatorLambdaParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_RefParameterOfStructHoldingReferenceOrSpan_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_SpanExtensionReceiverAndInSpanParameter_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_AccessorIndexerAndOperatorParameters_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_InitializerWritesPrimaryConstructorParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ExtensionReceiverParameters_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_IteratorAndAsyncBodies_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_InsideInterpolationLambdaQueryAndSwitch_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_StructGenericInterfaceAndPartialShapes_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_DynamicParameter_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_ParameterReassignedThenWritten_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_GeneratedCode_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0104", """
                                                            // <auto-generated/>
                                                            [Fprog.FunctionalCore]
                                                            public static class Generated
                                                            {
                                                                public static int Set(Box box) => box.Count = 1;
                                                            }
                                                            """ + Types, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0104_PointerParametersAndBodilessMembers_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_PureCoreWithMutatingCallsAndLocals_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_InAndRefReadonlyParameters_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_MutatedLocalCopiesOfParameterData_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0104_RefAndOutArgumentsIntoParameterObject_Reports()
    {
        return VerifyAsync("""
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
                                   var ok = Io.TryRead("1", out {|FP0104:box.Count|});
                                   return Io.Bump(ref {|FP0104:values[0]|}) + (ok ? box.Count : 0);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_OutArgumentOfBclMethodIntoParameterObject_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Reader
                           {
                               public static bool Read(string text, Box box) => int.TryParse(text, out {|FP0104:box.Count|});

                               public static bool ReadInto(string text, int[] slots) => int.TryParse(text, out {|FP0104:slots[0]|});

                               public static bool Deep(string text, Box box)
                                   => int.TryParse(text, out {|FP0104:box.Items[0].Inner!.Count|})
                                      && int.TryParse(text, out {|FP0104:(box.Items[0].Count)|});
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_RefArgumentToLocalFunctionConstructorAndSwap_Reports()
    {
        return VerifyAsync("""
                           public sealed class Cell
                           {
                               public Cell(ref int slot) => slot++;
                           }

                           public static class Lib
                           {
                               public static void Swap(ref int a, ref int b) => (a, b) = (b, a);
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Touch(Box box, int[] values, ref Point point)
                               {
                                   Lib.Swap(ref {|FP0104:values[0]|}, ref {|FP0104:values[1]|});
                                   _ = new Cell(ref {|FP0104:box.Count|});
                                   Bump(ref {|FP0104:point.X|});
                                   return Bump(ref {|FP0104:box.Inner!.Items[0].Count|});

                                   static int Bump(ref int value) => ++value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_RefReadonlyInAndLocalArguments_NoReport()
    {
        return VerifyAsync("""
                           public sealed class State
                           {
                               public int Count;
                               public long Total;
                           }

                           public static class Lib
                           {
                               public static int Peek(ref readonly int value) => value;
                               public static int PeekIn(in int value) => value;
                               public static int Bump(ref int value) => ++value;
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int ReadOnlyRef(State state) => Lib.Peek(ref state.Count);

                               public static int InArgument(State state) => Lib.PeekIn(in state.Count) + Lib.PeekIn(state.Count);

                               public static long VolatileRead(State state) => System.Threading.Volatile.Read(ref state.Total);

                               public static System.ReadOnlySpan<int> View(State state) => new(in state.Count);

                               public static int Locals(string text)
                               {
                                   int.TryParse(text, out var parsed);
                                   int local;
                                   int.TryParse(text, out local);
                                   var cells = new int[1];
                                   return parsed + local + Lib.Bump(ref cells[0]) + Lib.Bump(ref local);
                               }

                               public static bool OwnOutParameter(string text, out int value) => int.TryParse(text, out value);

                               public static int OwnRefParameter(ref int value, string text, int fallback)
                               {
                                   int.TryParse(text, out fallback);
                                   return Lib.Bump(ref value) + fallback;
                               }

                               public static int StructParameterByValue(Point point) => Lib.Bump(ref point.X);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_RefAndOutArgumentsOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static bool Read(string text, Box box, int[] values)
                                   => int.TryParse(text, out box.Count) && int.TryParse(text, out values[0]);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ReceiverChosenByConditionalCoalesceOrSwitch_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Booking
                           {
                               public static int Credit(bool toFirst, Box first, Box second, int amount)
                               {
                                   {|FP0104:(toFirst ? first : second).Count|} += amount;
                                   return first.Count + second.Count;
                               }

                               public static int Fallback(Box? preferred, Box fallback, int amount)
                               {
                                   {|FP0104:(preferred ?? fallback).Count|} += amount;
                                   return fallback.Count;
                               }

                               public static int ByIndex(int index, Box first, Box second, int amount)
                               {
                                   {|FP0104:(index switch { 0 => first, _ => second }).Count|} = amount;
                                   return amount;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ReceiverChosenWhenOnlyOneBranchIsParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Mixed(bool f, bool g, int index, Box box, Box other, Box third, int[] values)
                               {
                                   {|FP0104:(f ? new Box() : box).Count|} = 1;
                                   {|FP0104:(box.Inner ?? new Box()).Count|} = 2;
                                   {|FP0104:(f ? (g ? box : new Box()) : new Box()).X|}++;
                                   {|FP0104:(index switch { 0 => new Box(), 1 => box.Inner!, _ => throw new System.ArgumentException() }).Y|} = 3;
                                   {|FP0104:(f ? values : new int[1])[0]|} = 4;
                                   {|FP0104:(f ? box : other)?.Name|} = "x";
                                   {|FP0104:(f ? ref box : ref other).Count|} = 5;
                                   {|FP0104:(f ? box.Items[0] : third.Inner!).Count|} -= 6;
                                   ({|FP0104:(f ? box : other).X|}, {|FP0104:(f ? other : box).Y|}) = (7, 8);
                                   return int.TryParse("9", out {|FP0104:(f ? box : other).Count|}) ? box.Count : 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ReceiverChosenAmongFreshObjectsOrCopies_NoReport()
    {
        return VerifyAsync("""
                           public struct Counter : IHasCount
                           {
                               public int Count { get; set; }
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int BothFresh(bool f, int index, Box? maybe)
                               {
                                   (f ? new Box() : new Box()).Count = 1;
                                   (index switch { 0 => new Box(), _ => new Box { X = 1 } }).X = 2;
                                   var local = new Box();
                                   (f ? local : new Box()).Y = 3;
                                   (maybe?.Get() is null ? local : new Box()).Count = 4;
                                   return local.Count;
                               }

                               public static int BoxedBranches(bool f, Counter a, Counter b)
                               {
                                   (f ? (IHasCount)a : (IHasCount)b).Count = 1;
                                   (f ? a : (IHasCount)b).Count = 2;
                                   return a.Count;
                               }

                               public static int StructBranchesByValue(bool f, Point a, Point b)
                               {
                                   (f ? ref a : ref b).X = 1;
                                   return a.X;
                               }

                               public static int RebindParameter(bool f, Box a, Box b)
                               {
                                   (f ? ref a : ref b) = new Box();
                                   return a.Count + b.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ReceiverChosenByConditionalOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static int Credit(bool f, Box first, Box second, Box? maybe)
                               {
                                   (f ? first : second).Count = 1;
                                   (maybe ?? first).X++;
                                   return first.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughLocalAliasOfParameter_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Normalizer
                           {
                               public static int ViaLocal(Box box)
                               {
                                   var same = box;
                                   {|FP0104:same.Count|} = 0;
                                   return same.Count;
                               }

                               public static int ViaMember(Box box)
                               {
                                   var first = box.Inner;
                                   if (first is null) return 0;
                                   {|FP0104:first.Count|}++;
                                   var item = box.Items[0];
                                   {|FP0104:item.X|} = 1;
                                   var values = box.Values;
                                   {|FP0104:values[0]|} += 2;
                                   return first.Count;
                               }

                               public static int Chained(Box box, object any)
                               {
                                   var a = box;
                                   var b = a.Inner;
                                   {|FP0104:b!.Count|} = 1;
                                   var derived = box as DerivedBox;
                                   {|FP0104:derived!.X|} = 2;
                                   var maybe = box?.Inner?.Inner;
                                   {|FP0104:maybe!.Y|} = 3;
                                   object o = box;
                                   {|FP0104:((Box)o).Count|} = 4;
                                   var cast = (Box)any;
                                   {|FP0104:cast.Items[0]|} = new Box();
                                   return int.TryParse("5", out {|FP0104:a.Count|}) ? a.Count : 0;
                               }

                               public static System.Func<int> InLambda(Box box)
                               {
                                   var same = box;
                                   return () => {|FP0104:same.Count|} = 1;
                               }

                               public static int Deconstructed(Box box)
                               {
                                   var same = box;
                                   ({|FP0104:same.X|}, {|FP0104:same.Y|}) = (1, 2);
                                   return same.X;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughForeachVariable_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Normalizer
                           {
                               public static int Reset(Box box, List<Box> list, IEnumerable<Box> sequence)
                               {
                                   foreach (var item in box.Items)
                                       {|FP0104:item.Count|} = 0;
                                   foreach (var item in list)
                                       {|FP0104:item.X|}++;
                                   foreach (Box item in sequence)
                                       {|FP0104:item.Name|} ??= "x";
                                   var items = box.Items;
                                   foreach (var item in items)
                                       {|FP0104:item.Items[0]|} = item;
                                   return list.Count;
                               }

                               public static T[] Generic<T>(T[] values) where T : IHasCount
                               {
                                   foreach (var value in values)
                                       {|FP0104:value.Count|} = 0;
                                   return values;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughPatternVariable_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Normalizer
                           {
                               public static int ViaIs(object any, Box box)
                               {
                                   if (any is Box b)
                                       {|FP0104:b.Count|} = 1;
                                   if (any is Box { Count: > 0 } c and { X: 0 })
                                       {|FP0104:c.X|} = 2;
                                   if (any is (DerivedBox d))
                                       {|FP0104:d.Y|} = 3;
                                   if (box.Inner is { } inner)
                                       {|FP0104:inner.Count|}++;
                                   if (any is Box[] and [var head, ..] array)
                                       {|FP0104:array[0]|} = head;
                                   return 0;
                               }

                               public static int ViaCase(object any)
                               {
                                   switch (any)
                                   {
                                       case Box { Count: 0 } b when b.X > 0:
                                           {|FP0104:b.X|} = 1;
                                           return 1;
                                       default:
                                           return 0;
                                   }
                               }

                               public static int ViaArm(object any) => any switch
                               {
                                   Box b => {|FP0104:b.Count|} = 2,
                                   _ => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughLocalHoldingFreshObjectOrCopy_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Linq;

                           public sealed record Tag(string Name)
                           {
                               public int[] Slots { get; init; } = [];
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Copies(int[] values, IEnumerable<int> source, ImmutableArray<int> frozen, List<int> list)
                               {
                                   var copy = values.ToArray();
                                   copy[0] = 1;
                                   var fromSource = source.ToList();
                                   fromSource[0] = 2;
                                   var builder = frozen.ToBuilder();
                                   builder[0] = 3;
                                   var tail = values[1..];
                                   tail[0] = 4;
                                   var part = list[1..];
                                   part[0] = 5;
                                   return copy[0] + fromSource[0] + builder[0] + tail[0] + part[0];
                               }

                               public static int Fresh(Box box, Tag tag)
                               {
                                   var fresh = new Box { Count = box.Count };
                                   fresh.Count++;
                                   var renamed = tag with { Slots = [1] };
                                   renamed.Slots[0] = 2;
                                   Box[] array = [box];
                                   array[0] = new Box();
                                   return fresh.Count + renamed.Slots[0];
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughLocalThatIsWrittenAgain_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Reassigned(Box box, object any)
                               {
                                   var line = box;
                                   line = new Box();
                                   line.Count = 5;
                                   var pair = box;
                                   (pair, _) = (new Box(), 1);
                                   pair.X = 1;
                                   var maybe = box.Inner;
                                   maybe ??= new Box();
                                   maybe.Y = 2;
                                   var renewed = box;
                                   Renew(out renewed);
                                   renewed.Count = 3;
                                   var target = box;
                                   ref var slot = ref target;
                                   slot = new Box();
                                   target.Count = 4;
                                   var captured = box;
                                   System.Action reset = () => captured = new Box();
                                   captured.X = 5;
                                   if (any is Box b)
                                   {
                                       b = new Box();
                                       b.Count = 6;
                                   }

                                   Box late;
                                   late = box;
                                   late.Count = 7;
                                   return line.Count;
                               }

                               private static bool Renew(out Box box)
                               {
                                   box = new Box();
                                   return true;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughStructCopyOrBoxedCopy_NoReport()
    {
        return VerifyAsync("""
                           public struct Counter : IHasCount
                           {
                               public int Count { get; set; }
                           }

                           public sealed class Holder
                           {
                               public Counter C;
                               public Counter[] All = new Counter[1];
                               public Point P;
                           }

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Copies(Holder holder, Point point, object any)
                               {
                                   var moved = point;
                                   moved.X++;
                                   var inner = holder.P;
                                   inner.Y = 1;
                                   if (any is Point p)
                                   {
                                       p.X = 5;
                                   }

                                   foreach (var counter in holder.All)
                                   {
                                       var local = counter;
                                       local.Count = 2;
                                   }

                                   return moved.X + inner.Y;
                               }

                               public static int Boxed(Holder holder)
                               {
                                   if (holder.C is IHasCount c)
                                       c.Count = 1;
                                   foreach (IHasCount each in holder.All)
                                       each.Count = 2;
                                   IHasCount boxed = holder.C;
                                   boxed.Count = 3;
                                   object o = holder.P;
                                   return holder.C.Count + (o is Point q ? q.X : 0);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_ReadOnlyLoopsAndPatternsOverParameter_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public static class Ops
                           {
                               public static int Sum(List<Box> boxes, object any)
                               {
                                   var total = 0;
                                   foreach (var box in boxes)
                                       total += box.Count;
                                   if (any is Box b)
                                       total += b.X;
                                   var first = boxes[0];
                                   return total + first.Count + (any switch { Box c => c.Y, _ => 0 });
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughAliasOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static int Reset(Box box, object any)
                               {
                                   var same = box;
                                   same.Count = 0;
                                   foreach (var item in box.Items)
                                       item.X = 1;
                                   if (any is Box b)
                                       b.Y = 2;
                                   return same.Count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0104_WriteThroughAliasInUnionBody_ReportsOnce()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pets
                           {
                               public record Cat;

                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static int Reset(Box box)
                                   {
                                       var same = box;
                                       {|FP0104:same.Count|} = 0;
                                       return int.TryParse("1", out {|FP0104:box.Count|}) ? 1 : 0;
                                   }
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0104", source + Types, testContext.CancellationToken);
    }
}