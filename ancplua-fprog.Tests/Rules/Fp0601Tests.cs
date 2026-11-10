using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0601Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0601_IsKeywordOnValueOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool IsNumber<T>(T value) => value {|FP0601:is|} int;
        }
        """);

    [TestMethod]
    public Task FP0601_IsKeywordAgainstT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool Fits<T>(object value) => value {|FP0601:is|} T;
        }
        """);

    [TestMethod]
    public Task FP0601_AsKeywordOnValueOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static string? AsText<T>(T value) => value {|FP0601:as|} string;
        }
        """);

    [TestMethod]
    public Task FP0601_AsKeywordToTWithClassConstraint_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static T? Narrow<T>(object value) where T : class => value {|FP0601:as|} T;
        }
        """);

    [TestMethod]
    public Task FP0601_DeclarationPatternOnValueOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static int Number<T>(T value) => value is {|FP0601:int number|} ? number : 0;
        }
        """);

    [TestMethod]
    public Task FP0601_DeclarationPatternOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool Fits<T>(object value) => value is {|FP0601:T typed|} && typed is not null;
        }
        """);

    [TestMethod]
    public Task FP0601_SwitchStatementCasesOnT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static int Kind<T>(T value)
            {
                switch (value)
                {
                    case {|FP0601:int number|}:
                        return number;
                    case {|FP0601:string|}:
                        return 1;
                    default:
                        return 0;
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0601_SwitchExpressionArmsOnT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static int Kind<T>(T value) => value switch
            {
                {|FP0601:int number|} => number,
                {|FP0601:string|} => 1,
                _ => 0
            };
        }
        """);

    [TestMethod]
    public Task FP0601_SwitchCaseOfTOverObject_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static int Kind<T>(object value) => value switch
            {
                {|FP0601:T|} => 1,
                _ => 0
            };
        }
        """);

    [TestMethod]
    public Task FP0601_NegatedAndCombinedTypePatternsOnT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool NotText<T>(T value) => value is not {|FP0601:string|};

            public static bool Scalar<T>(T value) => value is {|FP0601:int|} or {|FP0601:long|};
        }
        """);

    [TestMethod]
    public Task FP0601_TypedPropertyPatternOnT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool NonEmptyText<T>(T value) => value is {|FP0601:string { Length: > 0 }|};
        }
        """);

    [TestMethod]
    public Task FP0601_ConstantPatternOnValueOfTIsATypeTest_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool IsZero<T>(T value) => value is {|FP0601:0|};
        }
        """);

    [TestMethod]
    public Task FP0601_TypeOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static string Name<T>() => {|FP0601:typeof(T)|}.Name;
        }
        """);

    [TestMethod]
    public Task FP0601_GetTypeOnValueOfT_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static string Name<T>(T value) where T : notnull => value.{|FP0601:GetType|}().Name;

            public static string? MaybeName<T>(T value) => value?.{|FP0601:GetType|}().Name;
        }
        """);

    [TestMethod]
    public Task FP0601_ClassStructNotNullNewConstraintsNameNoTypeSoStillUnconstrained_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool A<T>(T value) where T : class => value {|FP0601:is|} string;

            public static bool B<T>(T value) where T : struct => value {|FP0601:is|} int;

            public static bool C<T>(T value) where T : notnull => value {|FP0601:is|} int;

            public static bool D<T>(T value) where T : new() => value {|FP0601:is|} int;

            public static string E<T>() where T : unmanaged => {|FP0601:typeof(T)|}.Name;
        }
        """);

    [TestMethod]
    public Task FP0601_ConstraintOnlyToAnotherUnconstrainedTypeParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Inspect
        {
            public static bool Same<T, TBase>(T value) where T : TBase => value {|FP0601:is|} int;
        }
        """);

    [TestMethod]
    public Task FP0601_ClassTypeParameterInGenericCoreType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Box<T>
        {
            public Box(T value) => Value = value;

            public T Value { get; }

            public bool HoldsText => Value {|FP0601:is|} string;
        }
        """);

    [TestMethod]
    public Task FP0601_InLambdaAndLocalFunctionInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Closures
        {
            public static System.Func<T, bool> Lambda<T>() => value => value {|FP0601:is|} int;

            public static bool Local(object value)
            {
                return Check<string>(value);

                static bool Check<TItem>(object item) => item {|FP0601:is|} TItem;
            }
        }
        """);

    [TestMethod]
    public Task FP0601_InTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static string Name<T>() => {|FP0601:typeof(T)|}.Name;
            }
        }
        """);

    [TestMethod]
    public Task FP0601_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static bool IsNumber<T>(T value) => value {|FP0601:is|} int;
        }
        """);

    [TestMethod]
    public Task FP0601_OutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static bool IsNumber<T>(T value) => value is int;

            public static string? AsText<T>(T value) => value as string;

            public static int Number<T>(T value) => value is int number ? number : 0;

            public static string Name<T>() => typeof(T).Name;

            public static string Runtime<T>(T value) where T : notnull => value.GetType().Name;

            public static int Kind<T>(T value) => value switch { int number => number, _ => 0 };
        }
        """);

    [TestMethod]
    public Task FP0601_NullChecksVarAndDiscardOnValueOfT_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Checks
        {
            public static bool Missing<T>(T value) => value is null;

            public static bool Present<T>(T value) => value is not null;

            public static bool Something<T>(T value) => value is { };

            public static T Same<T>(T value) => value is var copy ? copy : value;

            public static int Count<T>(T value) => value switch
            {
                null => 0,
                _ => 1
            };
        }
        """);

    [TestMethod]
    public Task FP0601_TypeParameterConstrainedToInterfaceIsNotUnconstrained_NoReport() => VerifyAsync("""
        public interface IShape { double Area { get; } }

        public sealed class Circle : IShape { public double Area => 3.0; }

        [Fprog.FunctionalCore]
        public static class Shapes
        {
            public static bool IsCircle<T>(T shape) where T : IShape => shape is Circle;

            public static string Name<T>() where T : IShape => typeof(T).Name;

            public static string Runtime<T>(T shape) where T : IShape => shape.GetType().Name;

            public static bool Via<T, TShape>(T shape) where T : TShape where TShape : IShape => shape is Circle;
        }
        """);

    [TestMethod]
    public Task FP0601_TypeTestsWithoutTypeParameterInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Concrete
        {
            public static bool IsNumber(object value) => value is int;

            public static string? AsText(object value) => value as string;

            public static int Number(object value) => value is int number ? number : 0;

            public static string Name() => typeof(int).Name;

            public static string Runtime(object value) => value.GetType().Name;
        }
        """);

    [TestMethod]
    public Task FP0601_DefaultNameofAndCastAreNotInTheRowList_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Generic
        {
            public static T? Empty<T>() => default(T);

            public static string Label<T>() => nameof(T);

            public static T Unbox<T>(object value) => (T)value;

            public static T Identity<T>(T value) => value;
        }
        """);

    [TestMethod]
    public Task FP0601_TestedTypeOnlyContainsTAsTypeArgument_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly struct Option<T> : System.IEquatable<Option<T>>
        {
            public Option(T value) => Value = value;

            public T Value { get; }

            public bool Equals(Option<T> other) => System.Collections.Generic.EqualityComparer<T>.Default.Equals(Value, other.Value);

            public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

            public override int GetHashCode() => System.Collections.Generic.EqualityComparer<T>.Default.GetHashCode(Value!);
        }
        """);

    [TestMethod]
    public Task FP0601_CompilerGeneratedRecordMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Pair<T>(T Left, T Right)
        {
            public Pair<T> Swap() => new(Right, Left);
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0601", source, testContext.CancellationToken);
}
