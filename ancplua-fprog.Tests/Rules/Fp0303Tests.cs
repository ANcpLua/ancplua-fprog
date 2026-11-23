using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0303Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0303_ReturnNullFromMethod_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static string{|FP0303:?|} Find(int key)
                               {
                                   if (key < 0)
                                       return {|FP0303:null|};

                                   return "found";
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ReturnDefaultLiteralAndDefaultOfType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static string{|FP0303:?|} Literal(int key)
                               {
                                   if (key < 0)
                                       return {|FP0303:default|};

                                   return "found";
                               }

                               public static object{|FP0303:?|} Typed() => {|FP0303:default(object)|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ExpressionBodiedMethod_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static string{|FP0303:?|} Nothing() => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ExpressionAndBlockLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Factories
                           {
                               public static System.Func<string{|FP0303:?|}> Expression() => () => {|FP0303:null|};

                               public static System.Func<int, string{|FP0303:?|}> Block() => key =>
                               {
                                   if (key < 0)
                                       return {|FP0303:default|};

                                   return "found";
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_LambdaInFieldInitializer_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Factories
                           {
                               public static readonly System.Func<string?> Empty = () => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_LocalFunction_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static int Length(string text)
                               {
                                   return Trimmed(text)?.Length ?? 0;

                                   static string{|FP0303:?|} Trimmed(string value)
                                   {
                                       if (value.Length == 0)
                                           return {|FP0303:null|};

                                       return value.Trim();
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullAndDefaultForNullableValueType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int{|FP0303:?|} None() => {|FP0303:null|};

                               public static int{|FP0303:?|} Zero() => {|FP0303:default|};

                               public static int{|FP0303:?|} Typed() => {|FP0303:default(int?)|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_DefaultOfValueTypeConvertedToNullableOrReference_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int{|FP0303:?|} Lifted() => {|FP0303:default(int)|};

                               public static object Boxed() => {|FP0303:default(int)|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_DefaultForNonNullableValueType_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Numbers
                           {
                               public static int Zero() => default;

                               public static int Typed() => default(int);

                               public static System.DateTime Epoch(bool reset) => reset ? default : System.DateTime.UnixEpoch;

                               public static System.Func<int> Factory() => () => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_AsyncTaskOfReferenceTypeReturnsNull_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Loader
                           {
                               public static async System.Threading.Tasks.Task<string{|FP0303:?|}> Load(System.Threading.Tasks.Task<int> pending)
                               {
                                   if (await pending < 0)
                                       return {|FP0303:null|};

                                   return "loaded";
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_AsyncTaskOfIntReturnsDefault_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Loader
                           {
                               public static async System.Threading.Tasks.Task<int> Load(System.Threading.Tasks.Task<int> pending)
                               {
                                   if (await pending < 0)
                                       return default;

                                   return 1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullInConditionalBranches_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Choice
                           {
                               public static string{|FP0303:?|} First(bool flag) => flag ? {|FP0303:null|} : "value";

                               public static string{|FP0303:?|} Second(bool flag)
                               {
                                   return flag ? "value" : {|FP0303:default|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullInSwitchExpressionArm_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Days
                           {
                               public static string{|FP0303:?|} Name(int day) => day switch
                               {
                                   0 => "Sunday",
                                   1 => {|FP0303:default|},
                                   _ => {|FP0303:null|}
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullOnRightSideOfCoalesce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Fallback
                           {
                               public static string{|FP0303:?|} Pick(string? first, bool flag) => first ?? (flag ? "second" : {|FP0303:null|});

                               public static string{|FP0303:?|} Other(string? first) => first ?? {|FP0303:default|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_DefaultOfUnconstrainedTypeParameter_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Generic
                           {
                               public static T Typed<T>() => {|FP0303:default(T)|};

                               public static T{|FP0303:?|} Literal<T>() => {|FP0303:default|};

                               public static T{|FP0303:?|} Nullable<T>() where T : struct => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_DefaultOfStructConstrainedTypeParameter_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Generic
                           {
                               public static T Typed<T>() where T : struct => default(T);

                               public static T Literal<T>() where T : unmanaged => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullForgivingLiteralAndOperator_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Lies
                           {
                               public static string Never() => {|FP0303:null|}{|FP0303:!|};

                               public static string Also() => {|FP0303:default|}{|FP0303:!|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_CastNullReportsAtNullLiteral_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Casts
                           {
                               public static object{|FP0303:?|} Cast() => (string?){|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_YieldReturnNullForReferenceElement_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Sequences
                           {
                               public static System.Collections.Generic.IEnumerable<string{|FP0303:?|}> Items()
                               {
                                   yield return "first";
                                   yield return {|FP0303:null|};
                                   yield return {|FP0303:default|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_YieldReturnDefaultForValueElement_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Sequences
                           {
                               public static System.Collections.Generic.IEnumerable<int> Items()
                               {
                                   yield return default;
                                   yield break;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_PropertyGetterAndOperatorCountAsMethods_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box
                           {
                               public string? Label => {|FP0303:null|};

                               public string? Tag
                               {
                                   get { return {|FP0303:default|}; }
                               }

                               public static string{|FP0303:?|} operator +(Box left, Box right) => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullPointerCountsAsNull_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static unsafe class Pointers
                           {
                               public static int* Nowhere() => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_InTypeNestedInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Outer
                           {
                               public static class Inner
                               {
                                   public static string{|FP0303:?|} Nothing() => {|FP0303:null|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_UnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           public static class Anywhere
                           {
                               public static string{|FP0303:?|} Nothing() => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_OutsideCore_NoReport()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static string? Nothing() => null;

                               public static string? Default() => default;

                               public static System.Func<string?> Factory() => () => null;

                               public static string? Choice(bool flag) => flag ? null : "value";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullUsedButNotReturned_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Checks
                           {
                               public static string Describe(string? text) => text is null ? "none" : text;

                               public static bool Missing(object? value) => value == null;

                               public static string Joined(string? text) => Or(text, null);

                               private static string Or(string? first, string? second) => first ?? second ?? "";

                               public static string Fallback(string? text) => text ?? "none";

                               public static int Length(string? text) => text?.Length ?? default;

                               public static System.Func<string, string> Identity() => value => value;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_CompilerGeneratedRecordMembers_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed record Person(string Name, string? Nickname);

                           [Fprog.FunctionalCore]
                           public abstract record Shape
                           {
                               public sealed record Circle(double Radius) : Shape;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_AnonymousMethod_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Factories
                           {
                               public static System.Func<string{|FP0303:?|}> Empty() => delegate { return {|FP0303:null|}; };

                               public static System.Func<int, object{|FP0303:?|}> Typed() => delegate (int key) { return key < 0 ? {|FP0303:default|} : key; };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_AsyncLambdaAndAsyncLocalFunction_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Loader
                           {
                               public static System.Func<System.Threading.Tasks.Task<string{|FP0303:?|}>> Lambda() => async () =>
                               {
                                   await System.Threading.Tasks.Task.Yield();
                                   return {|FP0303:null|};
                               };

                               public static System.Threading.Tasks.ValueTask<int{|FP0303:?|}> Local()
                               {
                                   return Inner();

                                   static async System.Threading.Tasks.ValueTask<int{|FP0303:?|}> Inner()
                                   {
                                       await System.Threading.Tasks.Task.Yield();
                                       return {|FP0303:default|};
                                   }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ExpressionTreeLambda_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Trees
                           {
                               public static System.Linq.Expressions.Expression<System.Func<string{|FP0303:?|}>> Empty() => () => {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_QuerySelectClauseIsALambda_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Queries
                           {
                               public static IEnumerable<string{|FP0303:?|}> Blanks(IEnumerable<string> words)
                                   => from word in words
                                      select word.Length == 0 ? {|FP0303:null|} : word;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_IndexerAndConversionOperators_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Box
                           {
                               public string? this[int index] => {|FP0303:null|};

                               public object? this[string key]
                               {
                                   get { return {|FP0303:default|}; }
                               }

                               public static implicit operator string{|FP0303:?|}(Box box) => {|FP0303:null|};

                               public static explicit operator int{|FP0303:?|}(Box box) => {|FP0303:default(int?)|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ExtensionBlockMembers_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class TextExtensions
                           {
                               extension(string text)
                               {
                                   public string? Blank => {|FP0303:null|};

                                   public string{|FP0303:?|} Head() => text.Length == 0 ? {|FP0303:default|} : text.Substring(0, 1);
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_RecordStructInterfaceAndGenericTypeMembers_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public readonly record struct Point(int X, int Y)
                           {
                               public string{|FP0303:?|} Label() => {|FP0303:null|};
                           }

                           [Fprog.FunctionalCore]
                           public static class Holder
                           {
                               public interface IShape
                               {
                                   string{|FP0303:?|} Name() => {|FP0303:null|};
                               }

                               public sealed class Box<T>
                               {
                                   public T{|FP0303:?|} Get() => {|FP0303:default|};

                                   public TOther{|FP0303:?|} Map<TOther>() where TOther : class => {|FP0303:null|};

                                   public TKey{|FP0303:?|} Key<TKey>() where TKey : notnull => {|FP0303:default(TKey)|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_PartialMethodAndPartialProperty_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed partial class Parts
                           {
                               public partial string{|FP0303:?|} Name();

                               public partial string{|FP0303:?|} Name() => {|FP0303:null|};

                               public partial string? Label { get; }

                               public partial string? Label => {|FP0303:default|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NestedConditionalsSwitchesAndParentheses_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Nested
                           {
                               public static string{|FP0303:?|} Deep(bool a, bool b, int c)
                                   => a
                                       ? (b ? ({|FP0303:null|}) : "x")
                                       : c switch
                                       {
                                           0 => b ? "y" : {|FP0303:default|},
                                           1 => (string?)"z" ?? {|FP0303:null|},
                                           _ => "w"
                                       };

                               public static string{|FP0303:?|} Parenthesized() { return ({|FP0303:null|}); }

                               public static string{|FP0303:?|} Trimmed(string? text) => text?.Trim() ?? {|FP0303:null|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_SpanOfQualifiedAliasedAndCastDefaults_Reports()
    {
        return VerifyAsync("""
                           using Text = System.String;

                           [Fprog.FunctionalCore]
                           public static class Spans
                           {
                               public static string{|FP0303:?|} Global() => {|FP0303:default(global::System.String)|};

                               public static string{|FP0303:?|} Alias() => {|FP0303:default(Text)|};

                               public static System.Collections.Generic.List<int>{|FP0303:?|} Generic() => {|FP0303:default(System.Collections.Generic.List<int>?)|};

                               public static string{|FP0303:?|} CastDefault() => (string?){|FP0303:default|};

                               public static string{|FP0303:?|} Commented()
                               {
                                   return /* nothing */ {|FP0303:default /* type */ (
                                       string)|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_FunctionPointerAndVoidPointer_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static unsafe class Pointers
                           {
                               public static delegate*<int> NoFunction() => {|FP0303:null|};

                               public static void* NoAddress() => {|FP0303:default|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_AsyncIteratorAndNullableValueElements_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Streams
                           {
                               public static async System.Collections.Generic.IAsyncEnumerable<string{|FP0303:?|}> Names()
                               {
                                   await System.Threading.Tasks.Task.Yield();
                                   yield return {|FP0303:null|};
                               }

                               public static System.Collections.Generic.IEnumerable<int{|FP0303:?|}> Numbers()
                               {
                                   yield return {|FP0303:default|};
                               }

                               public static System.Collections.IEnumerable Untyped()
                               {
                                   yield return {|FP0303:null|};
                               }

                               public static async System.Threading.Tasks.Task<int{|FP0303:?|}> Missing(System.Threading.Tasks.Task<int> pending)
                               {
                                   await pending;
                                   return {|FP0303:null|};
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_DynamicAndNullableDisabledContext_Reports()
    {
        return VerifyAsync("""
                           #nullable disable
                           [Fprog.FunctionalCore]
                           public static class Legacy
                           {
                               public static string Oblivious() => {|FP0303:null|};

                               public static dynamic Anything() => {|FP0303:default|};
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NestedLambdasReportOnlyInnermostNull_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Curried
                           {
                               public static System.Func<System.Func<System.Func<string{|FP0303:?|}>>> Make() => () => () => () => {|FP0303:null|};

                               public static System.Func<string{|FP0303:?|}> Pick(int day) => day switch
                               {
                                   0 => () => {|FP0303:default|},
                                   _ => static () => "x"
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_LambdasInInitializersAndPrimaryConstructorBase_Reports()
    {
        return VerifyAsync("""
                           public class Source(System.Func<string?> create)
                           {
                               public System.Func<string?> Create { get; } = create;
                           }

                           [Fprog.FunctionalCore]
                           public sealed class Empty() : Source(() => {|FP0303:null|})
                           {
                               public System.Func<string?>[] All { get; } = [() => {|FP0303:default|}, static () => "x"];

                               public System.Func<int, string?> Lookup { get; init; } = key => key < 0 ? {|FP0303:null|} : "x";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_CoreStructAndCoreNestedInShell_Reports()
    {
        return VerifyAsync("""
                           public static class Shell
                           {
                               public static string? Outside() => null;

                               [Fprog.FunctionalCore]
                               public readonly struct Inside
                               {
                                   public static string{|FP0303:?|} Nothing() => {|FP0303:null|};
                               }

                               public static class Sibling
                               {
                                   public static string? Outside() => default;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ValueTypesWhoseDefaultHoldsNothing_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           public enum Color { Red, Green }

                           public readonly record struct Money(decimal Amount, string Currency);

                           [Fprog.FunctionalCore]
                           public static class Values
                           {
                               public static Color First() => default;

                               public static Money Zero() => default(Money);

                               public static ImmutableArray<string> Nothing() => default;

                               public static System.ReadOnlySpan<char> Blank() => default;

                               public static KeyValuePair<string, string> Pair() => default;

                               public static System.Threading.Tasks.ValueTask<int> Ready() => default;

                               public static System.Func<Color> Factory() => () => default(Color);

                               public static T Struct<T>() where T : struct, System.Enum => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullHeldInLocalsInitializersAndArguments_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           [Fprog.FunctionalCore]
                           public sealed record Person(string Name, string? Nick = null)
                           {
                               public string? Alias { get; init; } = null;

                               public Person Forget() => this with { Nick = null, Alias = default };

                               public static Person Anonymous() => new("anonymous") { Alias = null };

                               public static string Describe(IEnumerable<string> names, string? fallback = null)
                               {
                                   string? found = null;
                                   foreach (var name in names)
                                       found ??= name;

                                   return found ?? fallback ?? "";
                               }

                               public static string Label(string? text) => text is null or "" ? "none" : $"{text}{null}";

                               public static bool IsMissing(string? text) => text == default;

                               public static System.Action Nothing() => () => { return; };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.FunctionalCore]
                           public static class Generated
                           {
                               public static string? Nothing() => null;

                               public static string? Default() => default;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullableMethodReturnWithoutBody_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public abstract partial class Shapes
                           {
                               public abstract string{|FP0303:?|} Name();

                               public abstract string? Label { get; }

                               [System.Runtime.InteropServices.DllImport("native")]
                               public static extern string{|FP0303:?|} Native();

                               partial void Hook();

                               public delegate string? Factory();

                               public interface IShape
                               {
                                   string{|FP0303:?|} Name();

                                   static abstract string{|FP0303:?|} Create();
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullAssignedInReturnedExpression_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Assignments
                           {
                               public static string{|FP0303:?|} Coalesce(string? text) => text ??= {|FP0303:null|};

                               public static string{|FP0303:?|} Simple(string? text)
                               {
                                   return text = {|FP0303:default|};
                               }

                               public static string Kept(string? text) => text ??= "none";
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NewNullableValueTypeReportsOnlyReturnAnnotation_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Probe
                           {
                               public static int{|FP0303:?|} Created() => new int?();
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ConstantHoldingNullReportsOnlyReturnAnnotation_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Probe
                           {
                               private const string? Nothing = null;

                               public static string{|FP0303:?|} Named() => Nothing;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_ReturnsInsideStatementsAndConstructorLocalFunctions_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public sealed class Statements : Statements.IShape
                           {
                               public interface IShape
                               {
                                   string? Name { get; }

                                   string{|FP0303:?|} Describe();
                               }

                               static Statements()
                               {
                                   _ = Local();

                                   static string{|FP0303:?|} Local() => {|FP0303:null|};
                               }

                               string? IShape.Name => {|FP0303:null|};

                               string{|FP0303:?|} IShape.Describe() => {|FP0303:default|};

                               public static string{|FP0303:?|} Day(int day)
                               {
                                   switch (day)
                                   {
                                       case 0:
                                           return {|FP0303:null|};
                                   }

                                   try
                                   {
                                       if (day > 6)
                                           return {|FP0303:default|};
                                   }
                                   finally
                                   {
                                       _ = day;
                                   }

                                   return "weekday";
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullableTupleElementsAndTypeArguments_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           using System.Collections.Immutable;

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static (string{|FP0303:?|} Name, ImmutableArray<(object{|FP0303:?|} Tag, int{|FP0303:?|} Count)> Data)[] Make()
                                   => [(null, [(null, null)])];

                               public static int Find()
                               {
                                   return Local().Count ?? 0;

                                   static (string{|FP0303:?|} Name, int{|FP0303:?|} Count) Local() => default;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullableReturnThroughLocalOrOtherMember_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Lookup
                           {
                               public static string{|FP0303:?|} Find(bool found)
                               {
                                   string? name = null;
                                   if (found) name = "found";
                                   return name;
                               }

                               public static ImmutableArray<string{|FP0303:?|}> Slots() => [null, default, "x"];

                               public static string{|FP0303:?|} First(IEnumerable<string> names) => names.FirstOrDefault();

                               public static string{|FP0303:?|} Cast(object value) => value as string;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullForgivingOutsideReturnAndNullableReturnInUnionBody_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           [Fprog.FunctionalCore]
                           public union Choice(int, string)
                           {
                               public string{|FP0303:?|} Pick(string? input) => input;

                               public int Length(string? text)
                               {
                                   var exact = text{|FP0303:!|};
                                   return exact.Length;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullableNonReturnPositionsAndLogicalNot_NoReport()
    {
        return VerifyAsync("""
                           #nullable enable
                           [Fprog.FunctionalCore]
                           public sealed class Checker
                           {
                               private readonly string? _fallback;

                               public Checker(string? fallback) => _fallback = fallback;

                               public string? Fallback => _fallback;

                               public delegate string? Read();

                               public static bool Invert(bool value) => !value;

                               public string Value(string? text)
                               {
                                   string? local = text;
                                   return local ?? Fallback ?? "";
                               }
                           }

                           public static class Shell
                           {
                               public static string? Unchecked(string? text) => text!;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0303_NullForgivingInTypeAndMethodAttributes_Reports()
    {
        return VerifyAsync("""
                           using System;

                           public sealed class NoteAttribute(string message) : Attribute
                           {
                               public string Message { get; } = message;
                           }

                           [Fprog.FunctionalCore]
                           [Note(null{|FP0303:!|})]
                           public sealed class Core
                           {
                               [Note(null{|FP0303:!|})]
                               public static int Size() => 1;
                           }

                           [Note(null!)]
                           public sealed class Shell;
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0303", source, testContext.CancellationToken);
    }
}