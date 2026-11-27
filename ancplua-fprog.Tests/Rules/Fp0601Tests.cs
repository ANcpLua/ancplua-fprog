using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0601Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0601_IsKeywordOnValueOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool IsNumber<T>(T value) => value {|FP0601:is|} int;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_IsKeywordAgainstT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool Fits<T>(object value) => value {|FP0601:is|} T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_AsKeywordOnValueOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static string? AsText<T>(T value) => value {|FP0601:as|} string;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_AsKeywordToTWithClassConstraint_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static T? Narrow<T>(object value) where T : class => value {|FP0601:as|} T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_DeclarationPatternOnValueOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static int Number<T>(T value) => value {|FP0601:is|} int number ? number : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_DeclarationPatternOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool Fits<T>(object value) => value {|FP0601:is|} T typed && typed is not null;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_SwitchStatementCasesOnT_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_SwitchExpressionArmsOnT_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_SwitchCaseOfTOverObject_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_NegatedAndCombinedTypePatternsOnT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool NotText<T>(T value) => value {|FP0601:is|} not string;

                               public static bool Scalar<T>(T value) => value {|FP0601:is|} int or long;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypedPropertyPatternOnT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool NonEmptyText<T>(T value) => value {|FP0601:is|} string { Length: > 0 };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ConstantPatternOnValueOfTIsNotATypeTest_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool IsZero<T>(T value) => value is 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypeOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static string Name<T>() => {|FP0601:typeof(T)|}.Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_GetTypeWithIncompleteMostDerivedConstraint_Reports()
    {
        return VerifyAsync("""
            public closed class Root<X>;
            public closed class Inner<X> : Root<X>;
            public sealed class Known<X> : Inner<X>;
            public sealed class Extra<X> : Inner<System.Collections.Generic.List<X>>;

            [Fprog.FunctionalCore]
            public static class Inspect
            {
                public static System.Type RuntimeType<T, X, Wide, Narrow>(T value)
                    where T : Wide, Narrow where Wide : Root<X> where Narrow : Inner<X>
                    => value.{|FP0601:GetType|}();
            }
            """);
    }

    [TestMethod]
    public Task FP0601_GetTypeOnValueOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static string Name<T>(T value) where T : notnull => value.{|FP0601:GetType|}().Name;

                               public static string? MaybeName<T>(T value) => value?.{|FP0601:GetType|}().Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_KeywordOnlyConstraints_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_ChainedTypeParameterConstraint_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool Same<T, TBase>(T value) where T : TBase => value {|FP0601:is|} int;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ClassTypeParameterInGenericCoreType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box<T>
                           {
                               public Box(T value) => Value = value;

                               public T Value { get; }

                               public bool HoldsText => Value {|FP0601:is|} string;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_InLambdaAndLocalFunctionInCore_Reports()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_InTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static string Name<T>() => {|FP0601:typeof(T)|}.Name;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_UnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static bool IsNumber<T>(T value) => value {|FP0601:is|} int;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_OutsideCore_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_NullChecksVarAndDiscardOnValueOfT_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_TypeParameterWithInterfaceConstraint_Reports()
    {
        return VerifyAsync("""
                           public interface IShape { double Area { get; } }

                           public sealed class Circle : IShape { public double Area => 3.0; }

                           [Fprog.FunctionalCore]
                           public static class Shapes
                           {
                               public static bool IsCircle<T>(T shape) where T : IShape => shape {|FP0601:is|} Circle;

                               public static string Name<T>() where T : IShape => {|FP0601:typeof(T)|}.Name;

                               public static string Runtime<T>(T shape) where T : IShape => shape.{|FP0601:GetType|}().Name;

                               public static bool Via<T, TShape>(T shape) where T : TShape where TShape : IShape => shape {|FP0601:is|} Circle;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_RuntimeTestsOfObject_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Concrete
                           {
                               public static bool IsNumber(object value) => value {|FP0601:is|} int;

                               public static string? AsText(object value) => value {|FP0601:as|} string;

                               public static int Number(object value) => value {|FP0601:is|} int number ? number : 0;

                               public static string Name() => typeof(int).Name;

                               public static string Runtime(object value) => value.{|FP0601:GetType|}().Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ExplicitUnboxingBesideDefaultAndNameof_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Generic
                           {
                               public static T? Empty<T>() => default(T);

                               public static string Label<T>() => nameof(T);

                               public static T Unbox<T>(object value) => {|FP0601:(T)value|};

                               public static T Identity<T>(T value) => value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_EqualityOverrideObjectContract_NoReport()
    {
        return VerifyAsync("""
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
    }

    [TestMethod]
    public Task FP0601_CompilerGeneratedRecordMembers_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Pair<T>(T Left, T Right)
                           {
                               public Pair<T> Swap() => new(Right, Left);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_SwitchStatementConstantAndGuardedCasesOnT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Codes
                           {
                               public static int Code<T>(T value)
                               {
                                   switch (value)
                                   {
                                       case null:
                                           return -1;
                                       case 0:
                                           return 0;
                                       case "zero":
                                           return 0;
                                       case {|FP0601:long wide|} when wide > 0:
                                           return 1;
                                       default:
                                           return 2;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypePatternBesideRelationalPatternOnT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Ranges
                           {
                               public static bool Positive<T>(T value) => value is > 0;

                               public static bool Small<T>(T value) => value {|FP0601:is|} int and < 10;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypePatternInPropertySubpatternOfTypeT_Reports()
    {
        return VerifyAsync("""
                           public sealed record Pair<T>(T Left, T Right);

                           [Fprog.FunctionalCore]
                           public static class Pairs
                           {
                               public static bool LeftIsNumber<T>(Pair<T> pair) => pair {|FP0601:is|} { Left: int };

                               public static int Sum<T>(Pair<T> pair) => pair switch
                               {
                                   { Left: {|FP0601:int left|}, Right: {|FP0601:int right|} } => left + right,
                                   _ => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_NullableOfStructConstrainedT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nullables
                           {
                               public static T? Narrow<T>(object value) where T : struct => value {|FP0601:as|} T?;

                               public static string Name<T>() where T : struct => {|FP0601:typeof(T?)|}.Name;

                               public static bool IsNumber<T>(T? value) where T : struct => value {|FP0601:is|} int;

                               public static string Runtime<T>(T? value) where T : struct => value.{|FP0601:GetType|}().Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_GetTypeNameAcrossThisCommentsAndLineBreaks_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Holder<T>(T value) where T : notnull
                           {
                               public T Value { get; } = value;

                               public string Name => this.Value.{|FP0601:GetType|}().Name;

                               public string Spread => Value
                                   . /* runtime */ {|FP0601:GetType|}
                                   ()
                                   .Name;

                               public string Forgiven => Value!.{|FP0601:GetType|}().FullName ?? "";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_FieldAndPropertyInitializersOfGenericCoreType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Cache<T>
                           {
                               public static readonly string Name = {|FP0601:typeof(T)|}.Name;

                               public static System.Func<object, bool> Accepts { get; } = candidate => candidate {|FP0601:is|} T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ConstructorInitializerAndPrimaryConstructorBaseArgument_Reports()
    {
        return VerifyAsync("""
                           public abstract class Flagged(bool flag)
                           {
                               public bool Flag { get; } = flag;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Derived<T>(T value) : Flagged(value {|FP0601:is|} int)
                           {
                               public T Value { get; } = value;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Chained<T>
                           {
                               public Chained(T value) : this(value {|FP0601:is|} string) { }

                               private Chained(bool isText) => IsText = isText;

                               public bool IsText { get; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_OperatorsAccessorsIteratorsAndAsync_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly struct Cell<T>(T content)
                           {
                               public T Content { get; } = content;

                               public static bool operator true(Cell<T> cell) => cell.Content {|FP0601:is|} int;

                               public static bool operator false(Cell<T> cell) => cell.Content {|FP0601:is|} not int;

                               public string Kind
                               {
                                   get
                                   {
                                       return Content?.{|FP0601:GetType|}().Name ?? "none";
                                   }
                               }

                               public static System.Collections.Generic.IEnumerable<bool> Flags(T item)
                               {
                                   yield return item {|FP0601:is|} string;
                               }

                               public static async System.Threading.Tasks.Task<bool> Later(T item)
                               {
                                   await System.Threading.Tasks.Task.Yield();
                                   return item {|FP0601:is|} string;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ExtensionBlockTypeParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Extensions
                           {
                               extension<T>(T source)
                               {
                                   public bool IsNumber => source {|FP0601:is|} int;

                                   public string Name() => {|FP0601:typeof(T)|}.Name;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_InterfaceDefaultMemberNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Holder
                           {
                               public interface IProbe<T>
                               {
                                   T Value { get; }

                                   bool IsText => Value {|FP0601:is|} string;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_QueryExpressionInterpolationAndNestedLambdas_Reports()
    {
        return VerifyAsync("""
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Queries
                           {
                               public static System.Collections.Generic.IEnumerable<T> Numbers<T>(System.Collections.Generic.IEnumerable<T> items)
                                   => from item in items where item {|FP0601:is|} int select item;

                               public static string Show<T>(T value) => $"{value {|FP0601:is|} string}";

                               public static System.Func<System.Func<T, string>> Deep<T>() => () => item =>
                               {
                                   return Inner();

                                   string Inner() => ((System.Func<string>)(() => {|FP0601:typeof(T)|}.Name))();
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypeParameterOfNonCoreOuterTypeUsedInCoreNestedType_Reports()
    {
        return VerifyAsync("""
                           public static class Shell<T>
                           {
                               [Fprog.FunctionalCore]
                               public static class Core
                               {
                                   public static bool IsText(T value) => value {|FP0601:is|} string;
                               }

                               public static bool Outside(T value) => value is string;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_PartialMethodInPartialCoreTypeAndAbstractExternMembers_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract partial class Probe
                           {
                               public abstract bool Check<T>(T value);

                               public static extern bool Native<T>(T value);

                               public static partial bool Partial<T>(T value);
                           }

                           public abstract partial class Probe
                           {
                               public static partial bool Partial<T>(T value) => value {|FP0601:is|} int;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_CastOfValueOfTBeforeTypeTest_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Boxed
                           {
                               public static string Runtime<T>(T value) where T : notnull => ((object)value).{|FP0601:GetType|}().Name;

                               public static bool IsNumber<T>(T value) => (object?)value {|FP0601:is|} int;

                               public static string? AsText<T>(T value) => (object?)value {|FP0601:as|} string;

                               public static int Number<T>(T value) => (object?)value {|FP0601:is|} int number ? number : 0;

                               public static int Kind<T>(T value) => (object?)value switch
                               {
                                   {|FP0601:int number|} => number,
                                   null => -1,
                                   _ => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_IdentityBoxingAndNullableUnwrapAreNullChecks_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Unwrap
                           {
                               public static int Count<T>(T? value) where T : struct => value is T present ? present.GetHashCode() : 0;

                               public static int Total<T>(T? value) where T : struct => value switch
                               {
                                   T => 1,
                                   null => 0
                               };

                               public static bool Has<T>(T? value) where T : notnull => value is T present && present is not null;

                               public static bool Present<T>(T value) => value is object;

                               public static object? Erase<T>(T value) => value as object;

                               public static bool Lifted<T>(T value) where T : class => value as T is not null;

                               public static T? SameNullable<T>(T? value) where T : struct => value as T?;

                               public static T? LiftToNullable<T>(T value) where T : struct => value as T?;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ConstrainedToBaseClassEnumDelegateOrGenericInterface_Reports()
    {
        return VerifyAsync("""
                           public abstract class Shape { }

                           public sealed class Square : Shape { }

                           [Fprog.FunctionalCore]
                           public static class Constrained
                           {
                               public static bool IsSquare<T>(T shape) where T : Shape => shape {|FP0601:is|} Square;

                               public static string Kind<T>(T value) where T : struct, System.Enum => value.{|FP0601:GetType|}().Name;

                               public static string Name<T>() where T : System.Delegate => {|FP0601:typeof(T)|}.Name;

                               public static bool Fits<T>(object value) where T : System.IComparable<T> => value {|FP0601:is|} T;

                               public static T? Narrow<T>(object value) where T : Shape => value {|FP0601:as|} T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_RealisticGenericFunctionalCore_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public abstract record Option<T>
                           {
                               private Option() { }

                               public sealed record Some(T Value) : Option<T>;

                               public sealed record None : Option<T>;

                               public TResult Match<TResult>(System.Func<T, TResult> some, System.Func<TResult> none) => this switch
                               {
                                   Some found => some(found.Value),
                                   _ => none()
                               };
                           }

                           [Fprog.FunctionalCore]
                           public static class Pure
                           {
                               public static ImmutableArray<TResult> Map<T, TResult>(ImmutableArray<T> items, System.Func<T, TResult> map)
                                   => [.. items.Select(map)];

                               public static int CountWhere<T>(ImmutableArray<T> items, System.Func<T, bool> keep)
                               {
                                   var count = 0;
                                   foreach (var item in items)
                                   {
                                       if (keep(item))
                                           count++;
                                   }

                                   return count;
                               }

                               public static bool Same<T>(T left, T right) => System.Collections.Generic.EqualityComparer<T>.Default.Equals(left, right);

                               public static string Describe<T>(Option<T> option) => option is Option<T>.Some { Value: var inner } ? $"Some {inner}" : "None";

                               public static bool Known<T>(Option<T> option) => option is Option<T>.Some { Value: _ };

                               public static string Label<T>(T value) => nameof(value.GetType);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_UnsafeSizeofAndPointerTypeof_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static unsafe class Native
                           {
                               public static int Size<T>() where T : unmanaged => sizeof(T);

                               public static string Pointer<T>() where T : unmanaged => typeof(T*).Name;

                               public static string Array<T>() => typeof(T[]).Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_DynamicValueTestedAgainstT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Dynamic
                           {
                               public static bool Fits<T>({|FP0601:dynamic|} value) => value {|FP0601:is|} T;

                               public static int Pick<T>({|FP0601:dynamic|} value) => value {|FP0601:is|} T typed ? typed!.GetHashCode() : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_CastValueOfTInSwitchStatementWithConstantCases_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Codes
                           {
                               public static int Code<T>(T value)
                               {
                                   switch ((object?)value)
                                   {
                                       case null:
                                           return -1;
                                       case 0:
                                           return 0;
                                       default:
                                           return 1;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ConstantCasesOverConcreteValueAndNullCaseOverT_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Codes
                           {
                               public static int Code<T>(T value, int count)
                               {
                                   switch (count)
                                   {
                                       case 0:
                                           return 0;
                                       case > 10:
                                           return 2;
                                   }

                                   switch (value)
                                   {
                                       case null:
                                           return -1;
                                       default:
                                           return 1;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TupleSwitchElementOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Tuples
                           {
                               public static int Kind<TLeft, TRight>(TLeft left, TRight right) => (left, right) switch
                               {
                                   ({|FP0601:int number|}, _) => number,
                                   (_, {|FP0601:string|}) => 1,
                                   (null, _) => -1,
                                   _ => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_SpanOfQualifiedGenericAndParenthesizedPatterns_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Spans
                           {
                               public static bool Qualified<T>(T value) => value {|FP0601:is|} global::System.Int32;

                               public static int Count<T>(T value) => value {|FP0601:is|} (System.Collections.Generic.List<int> list) ? list.Count : 0;

                               public static bool Neither<T>(T value) => value {|FP0601:is|} not (int or string);

                               public static string Name<T>() => {|FP0601:typeof(
                                   /* the parameter */ T)|}.Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_IsAgainstTypeParameterThatIsNoCaseOfUnionValue_NoReport()
    {
        return VerifyAsync("""
                           public sealed record Success<T>(T Value);

                           public sealed record Failure(string Reason);

                           public readonly union Result<T>(Success<T>, Failure);

                           [Fprog.FunctionalCore]
                           public static class Results
                           {
                               public static bool IsBare<T>(Result<T> result) => result is T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ConstantAndRelationalPatternsOnValueOfT_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static bool IsZero<T>(T value) => value is 0;

                               public static bool IsPositive<T>(T value) => value is > 0;

                               public static int Code<T>(T value)
                               {
                                   switch (value)
                                   {
                                       case "zero":
                                           return 0;
                                       default:
                                           return 1;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_CasePatternOfUnionWithTypeParameterCase_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public sealed record None;

                           public readonly union Option<T>(T, None);

                           [Fprog.FunctionalCore]
                           public static class Options
                           {
                               public static U Match<T, U>(Option<T> option, Func<T, U> some, Func<U> none) => option switch
                               {
                                   T value => some(value),
                                   None => none(),
                                   null => none(),
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_IsAgainstTypeParameterCaseOfUnionValue_NoReport()
    {
        return VerifyAsync("""
                           public sealed record None;

                           public readonly union Option<T>(T, None);

                           [Fprog.FunctionalCore]
                           public static class Options
                           {
                               public static bool HasValue<T>(Option<T> option) => option is T;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_PatternSyntaxAgainstTypeParameterThatIsNoCaseOfUnionValue_NoReport()
    {
        return VerifyAsync("""
                           public sealed record Success<T>(T Value);

                           public sealed record Failure(string Reason);

                           public readonly union Result<T>(Success<T>, Failure);

                           [Fprog.FunctionalCore]
                           public static class Results
                           {
                               public static bool NotBare<T>(Result<T> result) => result is not T;

                               public static int Bare<T>(Result<T> result) => result is T value ? value!.GetHashCode() : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypeNameCaseLabelsOnTAndOfT_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Labels
                           {
                               public static int Kind<T>(T value)
                               {
                                   switch (value)
                                   {
                                       case null:
                                           return -1;
                                       case 0:
                                           return 0;
                                       case {|FP0601:System.String|}:
                                           return 1;
                                       case {|FP0601:int|}:
                                           return 2;
                                       default:
                                           return 3;
                                   }
                               }

                               public static int Fits<T>(object item)
                               {
                                   switch (item)
                                   {
                                       case {|FP0601:T|}:
                                           return 1;
                                       default:
                                           return 0;
                                   }
                               }

                               public static bool SmallNumber<T>(T value) => value {|FP0601:is|} int and > 0 and < 10;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_NamedNegatedCombinedAndComputedConstantsOnT_NoReport()
    {
        return VerifyAsync("""
                           public enum Color { Red, Green }

                           [Fprog.FunctionalCore]
                           public static class Constants
                           {
                               private const int Zero = 0;

                               public static bool IsZero<T>(T value) => value is Zero;

                               public static bool NotZero<T>(T value) => value is not 0;

                               public static bool Small<T>(T value) => value is 0 or 1 or (> 5 and < 10);

                               public static bool Red<T>(T value) => value is Color.Red;

                               public static int Code<T>(T value) => value switch
                               {
                                   "a" or "b" => 1,
                                   >= 2.5 => 2,
                                   null => -1,
                                   _ => 0
                               };

                               public static int Sum<T>(T value)
                               {
                                   switch (value)
                                   {
                                       case 1 + 2:
                                           return 3;
                                       case Color.Green:
                                           return 1;
                                       default:
                                           return 0;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_OwnCaseTypesOfUnionInEveryPatternForm_NoReport()
    {
        return VerifyAsync("""
                           using System;

                           public sealed record None;

                           public readonly union Option<T>(T, None);

                           public union Result<T>(T, Exception);

                           [Fprog.FunctionalCore]
                           public static class Unions
                           {
                               public static bool Empty<T>(Option<T> option) => option is not T;

                               public static int Peek<T>(Option<T> option) => option is T value ? value!.GetHashCode() : 0;

                               public static string Describe<T>(Result<T> result) => result switch
                               {
                                   T value => $"Success: {value}",
                                   Exception error => $"Error: {error.Message}",
                                   null => "empty"
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_UnionAndConcreteStructOperandsAgainstTypeParameter_NoReport()
    {
        return VerifyAsync("""
                           public sealed record None;

                           public readonly union Option<T>(T, None);

                           public readonly struct Box<T>(T value)
                           {
                               public T Value { get; } = value;
                           }

                           [Fprog.FunctionalCore]
                           public static class Checks
                           {
                               public static bool Other<T, U>(Option<T> option) => option is U;

                               public static int Pick<T, U>(Option<T> option) => option switch
                               {
                                   U picked => picked!.GetHashCode(),
                                   _ => 0
                               };

                               public static int Inner<T>(Box<T> box) => box is T inner ? inner!.GetHashCode() : 0;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ObjectSwitchPatternsAtNestedPositions_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public sealed record Envelope(object Value);

                               public static int Code(object value) => value switch
                               {
                                   {|FP0601:string text|} => text.Length,
                                   {|FP0601:Envelope { Value: {|FP0601:int number|} }|} => number,
                                   _ => 0
                               };

                               public static int Statement(object value)
                               {
                                   switch (value)
                                   {
                                       case {|FP0601:int number|}: return number;
                                       default: return 0;
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ExplicitDowncastAndUnboxingFromObjectAndTypeParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static string Text<T>(T item) => {|FP0601:(string)(object)item|};

                               public static int Count(object item) => {|FP0601:(int)item|};

                               public static T Cast<T>(T item) => (T)item;

                               public static System.IConvertible Narrow<T>(T item) where T : class =>
                                   {|FP0601:(System.IConvertible)item|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_WrittenDynamicTypeAndDynamicRuntimeOperations_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static {|FP0601:dynamic|} Echo({|FP0601:dynamic|} value)
                               {
                                   {|FP0601:dynamic|} prior = value;
                                   var inferred = prior;
                                   return inferred;
                               }

                               public static System.Collections.Generic.IEnumerable<{|FP0601:dynamic|}[]> Wrap() => [];

                               public static bool Text({|FP0601:dynamic|} item) => item {|FP0601:is|} string;

                               public static string? AsText({|FP0601:dynamic|} item) => item {|FP0601:as|} string;

                               public static int Count({|FP0601:dynamic|} item) => {|FP0601:(int)item|};

                               public static string Kind({|FP0601:dynamic|} item) => item.{|FP0601:GetType|}().Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_NamesSpelledDynamicAndInferredDynamic_NoReport()
    {
        return VerifyAsync("""
                           public static class Feed { public static dynamic Load() => 1; }

                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public sealed class @dynamic { }
                               public static @dynamic Copy(@dynamic value) => value;
                               public static int Copy(int dynamic) => dynamic;
                               public static object Load()
                               {
                                   var inferred = Feed.Load();
                                   return inferred;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_DynamicInUnionBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Zoo
                           {
                               public sealed record Cat;
                               public sealed record Dog;

                               public union Pet(Cat, Dog)
                               {
                                   public static bool Contains({|FP0601:dynamic|} input) => input {|FP0601:is|} Pet;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_BoxingUpcastNumericAndNamedSourceConversions_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public class Base { }
                               public sealed class Derived : Base { }
                               public static Derived Down(Base value) => (Derived)value;
                               public static Base Up(Derived value) => (Base)value;
                               public static object Box<T>(T value) => (object)value!;
                               public static double Convert(int value) => (double)value;
                               public static string Type(int value) => value.GetType().Name;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_UnionValuesInTestsAndSwitchThroughUpcast_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public closed record Shape;
                               public sealed record Circle : Shape;
                               public sealed record Square : Shape;
                               public sealed record Cat;
                               public sealed record Dog;
                               public union Pet(Cat, Dog);

                               public static bool CircleShape<T>(T value) where T : Shape => value is Circle;
                               public static Circle? AsCircle<T>(T value) where T : Shape => (object)value as Circle;
                               public static string Kind<T>(T value) where T : Shape => value.GetType().Name;
                               public static int Test(Pet value) => ((object)value) switch { Cat => 1, _ => 2 };
                               public static bool IsCat(Pet value) => (object)value is Cat;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ReferencedUnionValuesThroughUpcasts_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0601", """
            #nullable enable
            using Domain;

            [Fprog.FunctionalCore]
            public static class Inspect
            {
                public static bool IsCat(Pet pet) => (object)pet is Cat;
                public static Circle? AsCircle<T>(T shape) where T : Shape => (object)shape as Circle;
                public static int Sides(Shape shape) => (object)shape switch
                {
                    Circle => 0,
                    Square => 4,
                    Triangle => 3,
                    _ => -1
                };
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0601_TypeParameterConstrainedToIncompleteClosedHierarchy_Reports(bool fromReference)
    {
        const string source = """
                              #nullable enable
                              using Domain;

                              [Fprog.FunctionalCore]
                              public static class Inspect
                              {
                                  public static bool IsMarked<T, TValue>(TValue value) where TValue : Root<T>
                                      => value {|FP0601:is|} Marked<T>;
                                  public static IMarked? AsMarked<T, TValue>(TValue value) where TValue : Root<T>
                                      => (object)value {|FP0601:as|} IMarked;
                                  public static int Tag<T, TValue>(TValue value) where TValue : Root<T>
                                      => value switch { {|FP0601:Marked<T>|} => 1, _ => 0 };
                              }
                              """;
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync(
                "FP0601", source, ReferencedAssemblySources.GenericClosedHierarchies, testContext.CancellationToken)
            : VerifyAsync(source + "\n" + ReferencedAssemblySources.GenericClosedHierarchies);
    }

    [TestMethod]
    public Task FP0601_OverrideAndExternalImplicitAndExplicitInterfaceContracts_NoReport()
    {
        return VerifyAsync("""
                           public interface IIndex { int this[object candidate] { get; } }

                           [Fprog.FunctionalCore]
                           public sealed class Token(int value) : System.IComparable, System.Collections.IEqualityComparer, IIndex
                           {
                               public int Value => value;
                               public override bool Equals(object? candidate) => candidate is Token other && Value == other.Value;
                               public override int GetHashCode() => value;

                               public int CompareTo(object? candidate) => candidate switch
                               {
                                   Token other => Value.CompareTo(other.Value),
                                   _ => 1
                               };

                               bool System.Collections.IEqualityComparer.Equals(object? left, object? right) =>
                                   left is Token first && right is Token second && first.Value == second.Value;

                               int System.Collections.IEqualityComparer.GetHashCode(object value) => ((Token)value).Value;

                               int IIndex.this[object candidate] => candidate is int index ? index : -1;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_LocalOrParameterOutsideItsOverrideContract_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Token(int value)
                           {
                               public int Value => value;

                               public override bool Equals(object? candidate)
                               {
                                   object local = Value;
                                   return local {|FP0601:is|} int && candidate is Token other && Value == other.Value;
                               }

                               public override int GetHashCode() => Value;

                               public bool Other()
                               {
                                   object candidate = Value;
                                   return candidate {|FP0601:is|} int;
                               }

                               public bool Equals(object? candidate, bool same) => candidate {|FP0601:is|} Token && same;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_ObjectParameterImplementingCoreInterface_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Domain
                           {
                               public interface ICheck { bool Fits(object value); }
                               public sealed class Check : ICheck
                               {
                                   public bool Fits(object value) => value {|FP0601:is|} string;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_UserMethodWithGetTypeName_NoReport()
    {
        return VerifyAsync("""
                           public class Value { public new int GetType() => 1; }

                           [Fprog.FunctionalCore]
                           public static class Inspect
                           {
                               public static int Read<T>(T item) where T : Value => item.GetType();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0601_TypeParameterConstrainedToIncompleteClosedUnionClass_Reports()
    {
        return VerifyAsync("""
            #nullable enable
            using Chains;

            [Fprog.FunctionalCore]
            public static class Format
            {
                public static bool IsEnd<T, V>(V value) where V : Link<T> => value {|FP0601:is|} End<T>;
                public static int Tag<T, V>(V value) where V : Link<T> => value switch { {|FP0601:End<T>|} => 1, _ => 2 };
            }

            namespace Chains
            {
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [Union]
                public closed class Link<T> : IUnion
                {
                    public Link(int number) => Value = number;
                    public Link(string text) => Value = text;
                    public object? Value { get; }
                }

                public sealed class End<T>() : Link<T>(0);
                public sealed class Extra<T>() : Link<List<T>>(0);
            }
            """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0601", source, testContext.CancellationToken);
    }
}
