using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0201Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0201_InstanceFieldInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Counter
                           {
                               private int {|FP0201:_count|};
                               public string {|FP0201:Name|} = "counter";

                               public int Next() => _count + Name.Length;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_EachOfSeveralDeclaratorsInOneDeclaration_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Point
                           {
                               public int {|FP0201:X|}, {|FP0201:Y|} = 1, {|FP0201:Z|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_StaticField_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Cache
                           {
                               private static int {|FP0201:_hits|};
                               public static string? {|FP0201:Last|} = null;

                               public static int Hits() => _hits;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_StructAndRecordStructFields_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_FieldInRecordClass_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Person(string Name)
                           {
                               public int {|FP0201:Age|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_VolatileField_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Flag
                           {
                               private volatile bool {|FP0201:_set|};

                               public bool IsSet() => _set;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldLikeEventHasMutableBackingField_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Button
                           {
                               public event System.Action? Clicked, Pressed;
                               public static event System.EventHandler? Created;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_RefFieldWithReadonlyReferentIsReassignable_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public ref struct Cursor
                           {
                               public ref readonly int {|FP0201:Current|};
                               public ref int {|FP0201:Target|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FixedSizeBuffer_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public unsafe struct Packet
                           {
                               public fixed byte {|FP0201:Data|}[4];
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldInPartialTypeAnnotatedOnOtherPart_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed partial class Ledger;

                           public sealed partial class Ledger
                           {
                               private int {|FP0201:_balance|};

                               public int Balance() => _balance;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldInTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_FieldUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_MutableFieldsOutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_ReadonlyAndConstFields_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_CompilerGeneratedBackingFields_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_EnumMembersAreConst_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Colors
                           {
                               public enum Color { Red, Green = 2, Blue }

                               public static Color Default() => Color.Red;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_EventsWithoutBackingField_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_LocalVariablesInCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0201_GeneratedCodeInCore_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public sealed class Generated
                           {
                               public int Value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_SpanIsIdentifierDespiteAttributesCommentsAndLineBreaks_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Annotated
                           {
                               [System.NonSerialized]
                               public global::System.Int32
                                   /* the count */ {|FP0201:Count|} /* trailing */;

                               public int {|FP0201:@event|};

                               public System.Collections.Generic.List<int>? {|FP0201:Items|} = new() { 1, 2 };

                               public System.Func<int, int> {|FP0201:Twice|} = x =>
                               {
                                   var local = x;
                                   return local * 2;
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_RequiredNewProtectedInternalAndThreadStaticModifiers_Reports()
    {
        return VerifyAsync("""
                           public class Base
                           {
                               public int Shadowed;
                           }

                           [Fprog.FunctionalCore]
                           public class Derived : Base
                           {
                               public required int {|FP0201:Id|};
                               public new int {|FP0201:Shadowed|};
                               protected internal static int {|FP0201:Shared|};
                               [System.ThreadStatic] private static int {|FP0201:_perThread|};
                               private static volatile int {|FP0201:_flag|};

                               public static int Read() => _perThread + _flag;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldsInGenericTypes_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box<T>
                               where T : notnull
                           {
                               public T {|FP0201:Value|} = default!;
                               public static Box<T>? {|FP0201:Empty|};

                               public sealed class Slot<TKey>
                                   where TKey : notnull
                               {
                                   public System.Collections.Generic.Dictionary<TKey, T>? {|FP0201:Map|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PointerFunctionPointerAndRefStructFields_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public unsafe struct Native
                           {
                               public int* {|FP0201:Pointer|};
                               public delegate*<int, int> {|FP0201:Callback|};
                               public readonly int* Fixed;
                           }

                           [Fprog.FunctionalCore]
                           public ref struct Window
                           {
                               public int {|FP0201:Length|};
                               public static int {|FP0201:Instances|};
                               public readonly int Start;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_TupleTypedFieldReportsOnlyFieldName_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Pairs
                           {
                               public (int Left, int Right) {|FP0201:Pair|};
                               public readonly (string Key, int Count) Entry = ("a", 1);

                               public int Sum() => Pair.Left + Pair.Right + Entry.Count;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldMatchingPositionalRecordParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Person(string Name, int Age)
                           {
                               public string {|FP0201:Name|} = Name;
                               public readonly int Age = Age;
                           }

                           [Fprog.FunctionalCore]
                           public readonly record struct Point(int X)
                           {
                               public readonly int X = X;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldInitializedFromPrimaryConstructorParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Account(decimal opening)
                           {
                               private decimal {|FP0201:_balance|} = opening;

                               public decimal Balance() => _balance;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldsInNamespacesFileLocalAndNestedRecords_Reports()
    {
        return VerifyAsync("""
                           namespace App.Domain
                           {
                               [Fprog.FunctionalCore]
                               file sealed class Hidden
                               {
                                   public int {|FP0201:Value|};
                               }

                               [Fprog.FunctionalCore]
                               public sealed record class Order(int Id)
                               {
                                   private static int {|FP0201:_next|};

                                   public sealed record Line(string Sku)
                                   {
                                       public decimal {|FP0201:Price|};
                                   }

                                   public record struct Tag(string Text)
                                   {
                                       public int {|FP0201:Weight|};
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldLikeEventsWithInitializerOverrideAndInInterface_NoReport()
    {
        return VerifyAsync("""
                           public abstract class Notifier
                           {
                               public abstract event System.Action? Changed;
                           }

                           [Fprog.FunctionalCore]
                           public class Concrete : Notifier
                           {
                               public override event System.Action? Changed;
                               public virtual event System.Action Started = delegate { };
                           }

                           [Fprog.FunctionalCore]
                           public struct Signal
                           {
                               public event System.Action? Fired;
                           }

                           [Fprog.FunctionalCore]
                           public static class Hub
                           {
                               public interface IBus
                               {
                                   static event System.Action? Published;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_FieldLikeEventIsNotAField_NoReport()
    {
        return VerifyAsync("""
                           #nullable enable
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Publisher
                           {
                               private readonly int _id = 1;

                               public event EventHandler? Changed;

                               public static event Action? Reset;

                               public int Id => _id;

                               public bool HasSubscribers => Changed is not null || Reset is not null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_DelegateFieldBesideFieldLikeEvent_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Timer
                           {
                               public System.Action? {|FP0201:OnTick|};
                               public static System.EventHandler? {|FP0201:OnReset|}, {|FP0201:OnStop|};

                               public event System.Action? Ticked;
                               public static event System.EventHandler? Reset;
                           }

                           [Fprog.FunctionalCore]
                           public struct Pulse
                           {
                               public System.Action? {|FP0201:Handler|};
                               public event System.Action? Fired;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_HandwrittenEventBackingField_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Store
                           {
                               private System.EventHandler? {|FP0201:_changed|};

                               public event System.EventHandler? Changed
                               {
                                   add => _changed += value;
                                   remove => _changed -= value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_ReadonlyDelegateFieldBesideFieldLikeEvent_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Relay
                           {
                               private readonly System.Action _noop = () => { };
                               public static readonly System.EventHandler? Empty = null;

                               public event System.Action? Forwarded;
                               public static event System.EventHandler? Closed;

                               public void Run() => _noop();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_ExternFieldLikeEvent_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Interop
                           {
                               public static extern event System.Action? Raised;
                               public extern event System.Action? Changed;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenByAMember_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Ticker(int {|FP0201:start|})
                           {
                               public int Next() => ++start;
                           }

                           [Fprog.FunctionalCore]
                           public struct Step(int {|FP0201:size|})
                           {
                               public int Grow() => size += 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_EveryKindOfWriteToPrimaryConstructorParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Writes(
                               int {|FP0201:simple|},
                               int {|FP0201:compound|},
                               string? {|FP0201:coalesce|},
                               int {|FP0201:prefix|},
                               int {|FP0201:postfix|},
                               int {|FP0201:left|},
                               int {|FP0201:right|},
                               int {|FP0201:parsed|},
                               int {|FP0201:swapped|},
                               int onlyRead)
                           {
                               public int Simple() => simple = 1;

                               public int Compound() => compound *= 2;

                               public string Coalesce() => coalesce ??= "x";

                               public int Prefix() => --prefix;

                               public int Postfix() => postfix++;

                               public int Deconstruct()
                               {
                                   (left, (right, _)) = (1, (2, 3));
                                   return left + right;
                               }

                               public bool Parse(string text) => int.TryParse(text, out parsed);

                               public int Swap(ref int other)
                               {
                                   System.Threading.Interlocked.Exchange(ref swapped, other);
                                   return swapped + onlyRead;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenManyTimesReportedOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Counter(int {|FP0201:count|}, string name)
                           {
                               public int Next() => ++count;

                               public int Previous() => --count;

                               public int Reset()
                               {
                                   count = 0;
                                   count += 1;
                                   return count;
                               }

                               public string Name => name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenInAccessorsOperatorsAndFinalizer_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Members(int {|FP0201:getter|}, int {|FP0201:setter|}, int {|FP0201:indexer|}, int {|FP0201:operand|}, int {|FP0201:handler|}, int {|FP0201:last|})
                           {
                               public int Get => ++getter;

                               public int Set { get => setter; init => setter = value; }

                               public int this[int i] { get { indexer += i; return indexer; } }

                               public static int operator +(Members m, int i) => m.Add(i);

                               public event System.Action? Changed { add => handler++; remove => handler--; }

                               private int Add(int i) => operand += i;

                               ~Members() => last = 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenInLambdaOrLocalFunction_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class InitLambda(int {|FP0201:count|})
                           {
                               public Func<int> Next { get; } = () => ++count;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class FieldLambda(int {|FP0201:count|})
                           {
                               private readonly Func<int> _next = () => count++;

                               public int Next() => _next();
                           }

                           [Fprog.FunctionalCore]
                           public sealed class LocalFunction(int {|FP0201:count|})
                           {
                               public int Next()
                               {
                                   return Bump();

                                   int Bump() => ++count;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public sealed class MethodLambda(int {|FP0201:count|})
                           {
                               public Func<int> Next() => () => count += 2;
                           }

                           public abstract class Base(Func<int> next)
                           {
                               public int Next() => next();
                           }

                           [Fprog.FunctionalCore]
                           public sealed class BaseLambda(int {|FP0201:count|}) : Base(() => ++count);
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterInGenericPartialNestedAndAssemblyCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public sealed class Box<T>(T {|FP0201:value|})
                               {
                                   public T Swap(T next)
                                   {
                                       var old = value;
                                       value = next;
                                       return old;
                                   }
                               }
                           }

                           [Fprog.FunctionalCore]
                           public partial class Split;

                           public partial class Split(int {|FP0201:count|})
                           {
                               public int Next() => ++count;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public readonly struct Reader(int start)
                           {
                               public int Start => start;
                           }

                           public struct Cursor(int {|FP0201:position|})
                           {
                               public int Advance() => position++;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenOnlyDuringConstruction_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Normalized(string? name, int low, int high)
                           {
                               public string Name { get; } = (name ??= "anonymous").Trim();
                               public int Low { get; } = low > high ? low = high : low;
                               public int High { get; } = high;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class WrittenInInitializer(int count)
                           {
                               private readonly int _first = count++;
                               public int Sum => _first + count;
                           }

                           public abstract class Base(int value)
                           {
                               public int Value => value;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class BaseArgument(int count) : Base(count++ + (count = 2))
                           {
                               public int Count => count;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class OutInInitializer(string text, int parsed)
                           {
                               public bool Ok { get; } = int.TryParse(text, out parsed);
                               public int Parsed => parsed;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterOnlyReadOrShadowed_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class InvoiceNumbers(int last)
                           {
                               public int Last => last;

                               public InvoiceNumbers Next() => new(last + 1);

                               public int Shadow(int last) => ++last;

                               public int Local()
                               {
                                   var copy = last;
                                   copy++;
                                   return copy;
                               }

                               public string Name() => nameof(last);

                               public int Passed() => Read(in last) + Plain(last);

                               private static int Read(in int value) => value;

                               private static int Plain(int value) => ++value;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Holder(System.Text.StringBuilder builder)
                           {
                               public int Append() => builder.Append('x').Length;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_PrimaryConstructorParameterWrittenInRecordOrOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Named(int Start)
                           {
                               public int Start { get; set; } = Start;

                               public int Next() => ++Start;
                           }

                           [Fprog.FunctionalCore]
                           public record struct Point(int x)
                           {
                               public int Move() => x += 1;
                           }

                           public sealed class Shell(int count)
                           {
                               public int Next() => ++count;
                           }

                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static int Run() => new Inner(1).Next();
                           }

                           public sealed class Inner(int count)
                           {
                               public int Next() => ++count;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_ClosuresQueriesAnonymousTypesAndLocalConstsInCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Pipeline
                           {
                               private static readonly System.Func<int, int> Twice = x => x * 2;

                               public static int[] Run(int[] values)
                               {
                                   var offset = 1;
                                   System.Func<int, int> shift = x => x + offset;
                                   var query = from v in values
                                               let doubled = Twice(v)
                                               where doubled > offset
                                               select new { Value = shift(doubled), Original = v };

                                   return query.Select(item => Local(item.Value)).ToArray();

                                   static int Local(int y)
                                   {
                                       const int step = 2;
                                       var acc = y;
                                       acc += step;
                                       return acc;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_BackingFieldsOfSettableAndPartialProperties_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public partial struct Tracked
                           {
                               public Tracked() { }

                               public int Value { get; set; }
                               public static int Count { get; set; }
                               public int Clamped { get => field; set => field = value < 0 ? 0 : value; }
                               public partial int Partial { get; set; }
                               public partial int Partial { get => field; set => field = value; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_ReadonlyFieldsOfMutableTypesAndInterfaceConstants_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Registry
                           {
                               private readonly int[] _items = [1, 2];
                               private static readonly System.Collections.Generic.List<string> Names = [];
                               private readonly System.Text.StringBuilder _builder = new();

                               public int Count() => _items.Length + Names.Count + _builder.Length;

                               public interface ILimits
                               {
                                   const int Max = 3;
                                   static readonly int Min = 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_GeneratedCodeAttributeInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                           public sealed class Emitted
                           {
                               public int Value;
                               public event System.Action? Changed;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0201_UnusualShapesInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static unsafe class Odd
                           {
                               public delegate int Map(int x);

                               [System.Flags]
                               public enum Bits : byte { None = 0, A = 1 << 0, B = A | 2 }

                               private static readonly System.Func<int, System.Func<int, System.Func<int, int>>> Curry =
                                   a => b => c => a + b + c;

                               [System.Runtime.InteropServices.DllImport("native")]
                               public static extern int Native(int* pointer);

                               public static T Identity<T>(T value)
                                   where T : allows ref struct
                                   => value;

                               public static int Apply() => Curry(1)(2)(3);

                               extension(int value)
                               {
                                   public int Doubled => value * 2;
                               }
                           }

                           [Fprog.FunctionalCore]
                           public abstract class Shape
                           {
                               public abstract double Area();
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0201", source, testContext.CancellationToken);
    }
}