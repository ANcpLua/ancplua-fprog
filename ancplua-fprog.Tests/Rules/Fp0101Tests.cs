using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0101Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0101_VoidMethodInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Ledger
                           {
                               public void {|FP0101:Record|}(int amount)
                               {
                               }

                               private static void {|FP0101:Log|}() { }

                               internal void {|FP0101:Generic|}<T>(T value) => System.GC.KeepAlive(value);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidExtensionMethodInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Extensions
                           {
                               public static void {|FP0101:Touch|}(this string text) => System.GC.KeepAlive(text);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidLocalFunctionInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Steps
                           {
                               public static int Run(int value)
                               {
                                   Ignore(value);
                                   return Twice(value);

                                   static void {|FP0101:Ignore|}(int x) => System.GC.KeepAlive(x);
                                   static int Twice(int x) => x * 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidLocalFunctionInsideAccessorAndConstructorInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Holder
                           {
                               public Holder()
                               {
                                   Init();

                                   static void {|FP0101:Init|}() { }
                               }

                               public int Value
                               {
                                   get
                                   {
                                       Noop();
                                       return 1;

                                       static void {|FP0101:Noop|}() { }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ExplicitInterfaceImplementationInCore_ReportsAtMethodName()
    {
        return VerifyAsync("""
                           public interface ISink
                           {
                               void Put(int value);
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Sink : ISink
                           {
                               void ISink.{|FP0101:Put|}(int value)
                               {
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_AbstractAndInterfaceMethodsInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Contracts
                           {
                               public interface IVisitor
                               {
                                   void {|FP0101:Visit|}(int value);
                               }

                               public abstract class Base
                               {
                                   public abstract void {|FP0101:Apply|}();

                                   public virtual void {|FP0101:Hook|}() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_OverrideOfVoidMethodSuchAsDisposeInCore_Reports()
    {
        return VerifyAsync("""
                           public abstract class Resource
                           {
                               public abstract void Release();
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Handle : Resource, System.IDisposable
                           {
                               public override void {|FP0101:Release|}() { }

                               public void {|FP0101:Dispose|}() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_AsyncVoidMethodInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Events
                           {
                               public static async void {|FP0101:OnClick|}() => await System.Threading.Tasks.Task.Yield();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_PartialMethodInCore_ReportsAtBothDeclarations()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed partial class Part
                           {
                               partial void {|FP0101:OnChanged|}();

                               partial void {|FP0101:OnChanged|}() { }

                               partial void {|FP0101:Declared|}();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMainInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Entry
                           {
                               public static void {|FP0101:Main|}() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidCompoundAssignmentAndIncrementOperatorsInCore_ReportsAtOperatorToken()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Counter
                           {
                               public int Value { get; private set; }

                               public void operator {|FP0101:+=|}(int amount) => Value += amount;

                               public void operator {|FP0101:++|}() => Value++;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodInTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static void {|FP0101:Fire|}() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static void {|FP0101:Fire|}() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodsOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public sealed class Shell : System.IDisposable
                           {
                               public void Dispose() { }

                               public static async void OnClick() => await System.Threading.Tasks.Task.Yield();

                               public static int Run()
                               {
                                   Ignore();
                                   return 0;

                                   static void Ignore() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ValueReturningMethodsInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static int Add(int a, int b) => a + b;

                               public static (int, int) Swap((int A, int B) pair) => (pair.B, pair.A);

                               public static System.ValueTuple Unit() => default;

                               public static int Twice(int value)
                               {
                                   return Double(value);

                                   static int Double(int x) => x * 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidEventTypeBesideConstructorsAndAccessorsInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box
                           {
                               private readonly int _value;

                               static Box() { }

                               public Box(int value) => _value = value;

                               ~Box() { }

                               public int Value
                               {
                                   get => _value;
                                   init => _value = value;
                               }

                               public int this[int index] => _value + index;

                               public event System.{|FP0101:EventHandler|}? Changed
                               {
                                   add { }
                                   remove { }
                               }

                               public static Box operator +(Box left, Box right) => new(left._value + right._value);

                               public static implicit operator int(Box box) => box._value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidDelegateDeclarationAndWrittenReturnTypes_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public delegate void {|FP0101:Notify|}(int value);

                               public static System.{|FP0101:Action|}<int> Make() => value => System.GC.KeepAlive(value);

                               public static {|FP0101:Notify|} MakeNotify() => static delegate(int value) { System.GC.KeepAlive(value); };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_CompilerGeneratedRecordMembersInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Circle(double Radius)
                           {
                               public double Area() => System.Math.PI * Radius * Radius;
                           }

                           [Fprog.FunctionalCore]
                           public readonly record struct Point(int X, int Y);
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMembersOfExtensionBlockInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class TextExtensions
                           {
                               extension(string text)
                               {
                                   public void {|FP0101:Touch|}() => System.GC.KeepAlive(text);

                                   public static void {|FP0101:Reset|}() { }

                                   public int Size => text.Length;

                                   public int Count() => text.Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_LocalFunctionsInLambdasOfInitializersBaseArgumentsAndQueriesInCore_Reports()
    {
        return VerifyAsync("""
                           using System.Linq;

                           public abstract record Seeded(int Seed);

                           [Fprog.FunctionalCore]
                           public sealed record Initializers(int Value) : Seeded(Compute(() =>
                           {
                               Mark();
                               return Value;

                               static void {|FP0101:Mark|}() { }
                           }))
                           {
                               private static readonly System.Func<int, int> Twice = value =>
                               {
                                   Ignore(value);
                                   return value * 2;

                                   static void {|FP0101:Ignore|}(int x) => System.GC.KeepAlive(x);
                               };

                               public System.Func<int> Answer { get; } = () =>
                               {
                                   Skip();
                                   return 42;

                                   static void {|FP0101:Skip|}() { }
                               };

                               private static int Compute(System.Func<int> f) => f() + Twice(1);

                               public static int Query(int[] values)
                                   => (from v in values
                                       select new System.Func<int>(() =>
                                       {
                                           Note(v);
                                           return v;

                                           static void {|FP0101:Note|}(int x) => System.GC.KeepAlive(x);
                                       })()).Sum();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_LocalFunctionsInIteratorsAsyncNestedLambdasAndUnsafeCodeInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Shapes
                           {
                               public static System.Collections.Generic.IEnumerable<int> Numbers()
                               {
                                   Start();
                                   yield return 1;

                                   static void {|FP0101:Start|}() { }
                               }

                               public static async System.Threading.Tasks.Task<int> LoadAsync()
                               {
                                   await System.Threading.Tasks.Task.Yield();
                                   Done();
                                   return 1;

                                   static void {|FP0101:Done|}() { }
                               }

                               public static int Outer()
                               {
                                   return Middle();

                                   static int Middle()
                                   {
                                       Inner();
                                       return 1;

                                       static void {|FP0101:Inner|}() { }
                                   }
                               }

                               public static int Lambdas() => new System.Func<int>(() => new System.Func<int>(() =>
                               {
                                   Deep(1);
                                   return 2;

                                   static void {|FP0101:Deep|}<T>(T value) where T : notnull => System.GC.KeepAlive(value);
                               })())();

                               public static int Fire()
                               {
                                   Later();
                                   return 0;

                                   static async void {|FP0101:Later|}() => await System.Threading.Tasks.Task.Yield();
                               }

                               public static unsafe int Pointer(int* p)
                               {
                                   Clear(p);
                                   return *p;

                                   static void {|FP0101:Clear|}(int* q) => *q = 0;
                               }

                               public static int Native()
                               {
                                   Beep();
                                   return 0;

                                   [System.Runtime.InteropServices.DllImport("native")]
                                   static extern void {|FP0101:Beep|}();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_LocalFunctionsInsideFinalizerIndexerEventAccessorOperatorsAndInitInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Members
                           {
                               private readonly int _value;

                               static Members()
                               {
                                   Boot();

                                   static void {|FP0101:Boot|}() { }
                               }

                               ~Members()
                               {
                                   Cleanup();

                                   static void {|FP0101:Cleanup|}() { }
                               }

                               public int this[int index]
                               {
                                   get
                                   {
                                       Probe(index);
                                       return _value;

                                       static void {|FP0101:Probe|}(int i) => System.GC.KeepAlive(i);
                                   }
                               }

                               public event System.{|FP0101:EventHandler|}? Changed
                               {
                                   add
                                   {
                                       Watch(value);

                                       static void {|FP0101:Watch|}(object? h) => System.GC.KeepAlive(h);
                                   }
                                   remove { }
                               }

                               public static Members operator +(Members l, Members r)
                               {
                                   Sum();
                                   return l;

                                   static void {|FP0101:Sum|}() { }
                               }

                               public static explicit operator int(Members m)
                               {
                                   Convert();
                                   return m._value;

                                   static void {|FP0101:Convert|}() { }
                               }

                               public int Value
                               {
                                   get => _value;
                                   init
                                   {
                                       Check(value);
                                       _value = value;

                                       static void {|FP0101:Check|}(int v) => System.GC.KeepAlive(v);
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodsInRecordsStructsAndNestedInterfacesInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Pair(int Left, int Right)
                           {
                               public void {|FP0101:Deconstruct|}(out int sum) => sum = Left + Right;
                           }

                           [Fprog.FunctionalCore]
                           public readonly record struct Money(decimal Amount)
                           {
                               public void {|FP0101:Audit|}() { }
                           }

                           [Fprog.FunctionalCore]
                           public readonly struct Vector
                           {
                               public readonly void {|FP0101:Inspect|}() { }
                           }

                           [Fprog.FunctionalCore]
                           public ref struct Cursor
                           {
                               public void {|FP0101:Clear|}() { }
                           }

                           [Fprog.FunctionalCore]
                           public static class Contracts
                           {
                               public interface IShape<TSelf> where TSelf : IShape<TSelf>
                               {
                                   static abstract void {|FP0101:Register|}();

                                   static virtual void {|FP0101:Reset|}() { }

                                   void {|FP0101:Draw|}() => Helper();

                                   private void {|FP0101:Helper|}() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_TopLevelInterfaceUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public interface IVisitor
                           {
                               void {|FP0101:Visit|}(int value);

                               int Count();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ExternConditionalNewSealedOverrideAndUnsafeMethodsInCore_Reports()
    {
        return VerifyAsync("""
                           public class ShellBase
                           {
                               public void Reset() { }

                               public virtual void Tick() { }
                           }

                           [Fprog.FunctionalCore]
                           public class Natives : ShellBase
                           {
                               [System.Runtime.InteropServices.DllImport("native")]
                               private static extern void {|FP0101:Beep|}(int frequency);

                               [System.Diagnostics.Conditional("DEBUG")]
                               private static void {|FP0101:Trace|}(string message) => System.GC.KeepAlive(message);

                               public new void {|FP0101:Reset|}() { }

                               public sealed override void {|FP0101:Tick|}() { }

                               private static unsafe void {|FP0101:Zero|}(int* pointer) => *pointer = 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_PartialMethodsWithAttributeOnAnotherPartInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed partial class Split
                           {
                               partial void {|FP0101:Changed|}(int value);

                               public partial void {|FP0101:Extended|}();
                           }

                           public sealed partial class Split
                           {
                               partial void {|FP0101:Changed|}(int value) => System.GC.KeepAlive(value);
                           }

                           public sealed partial class Split
                           {
                               public partial void {|FP0101:Extended|}() { }

                               public partial int Size { get; }

                               public partial int Size => 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_QualifiedAndAliasQualifiedExplicitInterfaceImplementationsInCore_Reports()
    {
        return VerifyAsync("""
                           using Sinks = Contracts;

                           namespace Contracts
                           {
                               public interface ISink<in T>
                               {
                                   void Put(T value);
                               }
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Sink : Contracts.ISink<int>, global::Contracts.ISink<string>, Sinks::ISink<long>
                           {
                               void Contracts.ISink<int>.{|FP0101:Put|}(int value) { }

                               void global::Contracts.ISink<string>.{|FP0101:Put|}(string value) { }

                               void Sinks::ISink<long>.{|FP0101:Put|}(long value) { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_CommentsLineBreaksAndVerbatimNameInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Layout
                           {
                               public static void
                                   /* name follows */ {|FP0101:Spread|} // trailing
                                   (int value) => System.GC.KeepAlive(value);

                               public static void {|FP0101:@event|}() { }

                               public static void /**/{|FP0101:Generic|}/**/<T>() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ExplicitCheckedAndDecrementOperatorsInCore_Reports()
    {
        return VerifyAsync("""
                           public interface IAccumulator
                           {
                               void operator +=(int amount);
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Accumulator : IAccumulator
                           {
                               public int Total { get; private set; }

                               void IAccumulator.operator {|FP0101:+=|}(int amount) => Total += amount;

                               public void operator {|FP0101:-=|}(int amount) => Total -= amount;

                               public void operator checked {|FP0101:-=|}(int amount) => Total = checked(Total - amount);

                               public void operator {|FP0101:--|}() => Total--;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodsInNestedGenericTypesInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Outer<T>
                           {
                               public sealed class Inner<TInner> where TInner : struct
                               {
                                   public void {|FP0101:Use|}<TValue>(T outer, TInner inner, TValue? value)
                                       where TValue : class => System.GC.KeepAlive((outer, inner, value));
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_CoreTypeNestedInShellType_Reports()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static void Print() { }

                               public static int Run()
                               {
                                   Local();
                                   return 0;

                                   static void Local() { }
                               }

                               [Fprog.FunctionalCore]
                               public static class Core
                               {
                                   public static void {|FP0101:Fire|}() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_TypeDerivedFromCoreType_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract class CoreBase
                           {
                               public abstract int Value();
                           }

                           public sealed class Shell : CoreBase
                           {
                               public override int Value() => 1;

                               public void Print() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_GeneratedCodeInCore_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static void Fire() { }

                               public static int Run()
                               {
                                   Local();
                                   return 0;

                                   static void Local() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_PointerFunctionPointerAndValueDelegateReturnTypesInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static unsafe class Pointers
                           {
                               public static void* Raw(void* pointer) => pointer;

                               public static delegate*<void> Function(delegate*<void> pointer) => pointer;

                               public static System.Func<int> Make()
                               {
                                   return Local;

                                   static int Local() => 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_TypicalFunctionalCoreCode_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Pricing
                           {
                               public sealed record Item(string Name, decimal Price, int Quantity);

                               public static decimal Total(ImmutableArray<Item> items)
                               {
                                   var total = 0m;
                                   foreach (var item in items)
                                       total += item.Price * item.Quantity;
                                   return total;
                               }

                               public static ImmutableArray<string> Names(ImmutableArray<Item> items)
                                   => [.. from item in items where item.Quantity > 0 orderby item.Name select item.Name];

                               public static string Describe(Item item) => item switch
                               {
                                   { Quantity: 0 } => "none",
                                   { Price: > 100m } => "expensive",
                                   _ => "regular",
                               };

                               public static System.Collections.Generic.IEnumerable<int> Counts(ImmutableArray<Item> items)
                               {
                                   foreach (var item in items)
                                       yield return item.Quantity;
                               }

                               public static System.Func<int, int> Adder(int amount) => value => value + amount;

                               public static Item Restock(Item item, int amount)
                               {
                                   return item with { Quantity = Add(item.Quantity) };

                                   int Add(int quantity) => quantity + amount;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_MemberWithGeneratedCodeAttributeInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Tool
                           {
                               [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                               public static void Fire() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_FileLocalTypeMarkedThroughAttributeAlias_Reports()
    {
        return VerifyAsync("""
                           using Core = Fprog.FunctionalCoreAttribute;

                           [Core]
                           file static class Hidden
                           {
                               public static void {|FP0101:Fire|}() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMembersInUnionBodyNestedInCoreClass_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public void {|FP0101:Feed|}() { }

                                   public int Legs()
                                   {
                                       Count();
                                       return 4;

                                       static void {|FP0101:Count|}() { }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodInTopLevelUnionUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public void {|FP0101:Feed|}() { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodInClassNestedInUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static class Keeper
                                   {
                                       public static void {|FP0101:Feed|}() { }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_LocalFunctionsInInitializerAccessorAndOperatorOfUnionInNamespaceUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           namespace Pets
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   private static readonly System.Func<int, int> Twice = value =>
                                   {
                                       Ignore(value);
                                       return value * 2;

                                       static void {|FP0101:Ignore|}(int x) => System.GC.KeepAlive(x);
                                   };

                                   public static int Size
                                   {
                                       get
                                       {
                                           Probe();
                                           return Twice(1);

                                           static void {|FP0101:Probe|}() { }
                                       }
                                   }

                                   public static Pet operator +(Pet left, Pet right)
                                   {
                                       Sum();
                                       return left;

                                       static void {|FP0101:Sum|}() { }
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_RecordBaseArgumentAndNestedUnionInsideUnionBodyInCore_Reports()
    {
        return VerifyAsync("""
                           public abstract record Seeded(int Seed);

                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public sealed record Tag(int Value) : Seeded(Compute(() =>
                                   {
                                       Mark();
                                       return Value;

                                       static void {|FP0101:Mark|}() { }
                                   }));

                                   public union Inner(Cat, Dog)
                                   {
                                       public void {|FP0101:Pat|}() { }
                                   }

                                   private static int Compute(System.Func<int> f) => f();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ValueReturningMembersInUnionBodyInCore_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public record Cat;
                               public record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public int Legs()
                                   {
                                       return Count();

                                       static int Count() => 4;
                                   }

                                   public static class Keeper
                                   {
                                       public static int Feed() => 1;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMembersInUnionBodyOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public record Cat;
                           public record Dog;

                           public union Pet(Cat, Dog)
                           {
                               public void Feed() { }

                               public int Legs()
                               {
                                   Count();
                                   return 4;

                                   static void Count() { }
                               }

                               public static class Keeper
                               {
                                   public static void Feed() { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_DelegateDeclarationInAssemblyCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public delegate void {|FP0101:Observe|}(string text);
                           public delegate int Measure(System.{|FP0101:Action|} callback);
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegateParameterIncludingAlias_Reports()
    {
        return VerifyAsync("""
                           using Notify = System.Action<int>;

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Measure({|FP0101:Notify|} callback, int count)
                               {
                                   callback(count);
                                   return count;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegateLocal_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Measure()
                               {
                                   global::System.{|FP0101:Action|} callback = static () => { };
                                   callback();
                                   return 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegateField_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               private static readonly System.{|FP0101:Action|} Notify = static () => { }, Audit = static () => { };
                               public static int Measure() { Notify(); Audit(); return 1; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegateProperty_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static System.{|FP0101:Action|} Callback { get; } = static () => { };

                               public static System.{|FP0101:Action|} Make => static delegate { };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegateTypeArguments_Reports()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static ImmutableArray<{|FP0101:Action|}> Empty => [];

                               public static int Run() => Count<{|FP0101:Action|}>(static () => { });
                               private static int Count<T>(T item) => 1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_WrittenDelegatesInTupleAndFunctionPointerTypes_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           using Notify = System.Action<int>;

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static (global::System.{|FP0101:Action|}?[], {|FP0101:Notify|}) Read(
                                   (global::System.{|FP0101:Action|}?[], {|FP0101:Notify|}) source) => source;

                               public static unsafe delegate*<{|FP0101:Notify|}, int> Pointer(
                                   delegate*<{|FP0101:Notify|}, int> pointer) => pointer;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidLambdaPassedToOutsideMember_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver { public static int With(Action action) { action(); return 1; } }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run() => Driver.With(static () {|FP0101:=>|} { });
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidAnonymousMethodPassedToOutsideMember_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver { public static int With(Action action) { action(); return 1; } }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run() => Driver.With(static {|FP0101:delegate|} { });
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidMethodGroupPassedToOutsideMember_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver
                           {
                               public static int With(Action action) { action(); return 1; }
                               public static void Notify<T>() { }
                           }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run() => Driver.With(Driver.{|FP0101:Notify|}<int>);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_NaturalVoidDelegateTypeInVarLocal_Reports()
    {
        return VerifyAsync("""
                           public static class Driver { public static void Notify(int value) { } }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run()
                               {
                                   var callback = static (int _) {|FP0101:=>|} { };
                                   var reporting = Driver.{|FP0101:Notify|};
                                   callback(1);
                                   reporting(2);
                                   return 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_LambdaBranchesWithWrittenDelegateTarget_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static {|FP0101:Action|} Choose(bool first) => first
                                   ? static () => { }
                                   : static delegate { };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidDelegateArgumentBoundToWrittenCoreParameter_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run() => With(static () => { });
                               private static int With({|FP0101:Action|} callback) { callback(); return 1; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ExplicitDelegateConversionOutsideWrittenPositions_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver { public static void Notify() { } }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public static int Run()
                               {
                                   var cast = (Action)Driver.Notify;
                                   var constructed = new Action(Driver.Notify);
                                   cast();
                                   constructed();
                                   return 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_VoidDelegateAndItsUsesOutsideCore_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver
                           {
                               public delegate void Notice(int number);
                               public static Action Get() => static () => { };
                               public static Notice Create() => static delegate { };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_ValueReturningDelegateDeclarationAndConversions_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver
                           {
                               public static int With(Func<int> f) => f();
                               public static int One() => 1;
                           }

                           [Fprog.FunctionalCore]
                           public static class Callbacks
                           {
                               public delegate int Next();
                               public static Next Explicit() => static () => 2;

                               public static int Run()
                               {
                                   var inferred = static () => 3;
                                   return inferred() + Driver.With(static delegate { return 3; }) + Driver.With(Driver.One);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_DelegateDeclarationAndConversionsInUnionBody_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public static class Driver { public static int With(Action callback) { callback(); return 0; } }

                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public sealed record Cat;
                               public sealed record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public delegate void {|FP0101:Notify|}();
                                   public static {|FP0101:Notify|} Callback() => static () => { };
                                   public static int Run() => Driver.With(static () {|FP0101:=>|} { });
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_DelegateDeclarationWithOwnMarker_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public delegate void {|FP0101:Observe|}(string text);

                           [Fprog.FunctionalCore]
                           public delegate int Measure(System.{|FP0101:Action|} callback);
                           """);
    }

    [TestMethod]
    public Task FP0101_PrimaryConstructorAndFieldLikeEventDelegateTypes_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Callbacks({|FP0101:Action|} initial)
                           {
                               public event {|FP0101:Action|} Changed = initial;

                               public int Count() { Changed(); return 1; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0101_EventAndPropertyAccessorValueHasWrittenType_Reports()
    {
        return VerifyAsync("""
                           using System;

                           [Fprog.FunctionalCore]
                           public sealed class Callbacks
                           {
                               public event {|FP0101:Action|} Changed
                               {
                                   add { value = static () => { }; value(); }
                                   remove { }
                               }

                               public {|FP0101:Action|} Callback
                               {
                                   set { value = static () => { }; value(); }
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0101", source, testContext.CancellationToken);
    }
}
