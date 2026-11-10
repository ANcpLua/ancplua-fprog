using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0101Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0101_VoidMethodInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_VoidExtensionMethodInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Extensions
        {
            public static void {|FP0101:Touch|}(this string text) => System.GC.KeepAlive(text);
        }
        """);

    [TestMethod]
    public Task FP0101_VoidLocalFunctionInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_VoidLocalFunctionInsideAccessorAndConstructorInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_ExplicitInterfaceImplementationInCore_ReportsAtMethodName() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_AbstractAndInterfaceMethodsInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_OverrideOfVoidMethodSuchAsDisposeInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_AsyncVoidMethodInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Events
        {
            public static async void {|FP0101:OnClick|}() => await System.Threading.Tasks.Task.Yield();
        }
        """);

    [TestMethod]
    public Task FP0101_PartialMethodInCore_ReportsAtBothDeclarations() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Part
        {
            partial void {|FP0101:OnChanged|}();

            partial void {|FP0101:OnChanged|}() { }

            partial void {|FP0101:Declared|}();
        }
        """);

    [TestMethod]
    public Task FP0101_VoidMainInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Entry
        {
            public static void {|FP0101:Main|}() { }
        }
        """);

    [TestMethod]
    public Task FP0101_VoidCompoundAssignmentAndIncrementOperatorsInCore_ReportsAtOperatorToken() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Counter
        {
            public int Value { get; private set; }

            public void operator {|FP0101:+=|}(int amount) => Value += amount;

            public void operator {|FP0101:++|}() => Value++;
        }
        """);

    [TestMethod]
    public Task FP0101_VoidMethodInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static void {|FP0101:Fire|}() { }
            }
        }
        """);

    [TestMethod]
    public Task FP0101_VoidMethodUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static void {|FP0101:Fire|}() { }
        }
        """);

    [TestMethod]
    public Task FP0101_VoidMethodsOutsideCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_ValueReturningMethodsInCore_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0101_ConstructorsDestructorsAndAccessorsInCore_NoReport() => VerifyAsync("""
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

            public event System.EventHandler? Changed
            {
                add { }
                remove { }
            }

            public static Box operator +(Box left, Box right) => new(left._value + right._value);

            public static implicit operator int(Box box) => box._value;
        }
        """);

    [TestMethod]
    public Task FP0101_VoidLambdaAndVoidDelegateTypeInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Callbacks
        {
            public delegate void Notify(int value);

            public static System.Action<int> Make() => value => System.GC.KeepAlive(value);

            public static Notify MakeNotify() => static delegate(int value) { System.GC.KeepAlive(value); };
        }
        """);

    [TestMethod]
    public Task FP0101_CompilerGeneratedRecordMembersInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Circle(double Radius)
        {
            public double Area() => System.Math.PI * Radius * Radius;
        }

        [Fprog.FunctionalCore]
        public readonly record struct Point(int X, int Y);
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0101", source, testContext.CancellationToken);
}
