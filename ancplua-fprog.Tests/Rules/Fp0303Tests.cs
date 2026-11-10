using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0303Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0303_ReturnNullFromMethod_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lookup
        {
            public static string? Find(int key)
            {
                if (key < 0)
                    return {|FP0303:null|};

                return "found";
            }
        }
        """);

    [TestMethod]
    public Task FP0303_ReturnDefaultLiteralAndDefaultOfType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lookup
        {
            public static string? Literal(int key)
            {
                if (key < 0)
                    return {|FP0303:default|};

                return "found";
            }

            public static object? Typed() => {|FP0303:default(object)|};
        }
        """);

    [TestMethod]
    public Task FP0303_ExpressionBodiedMethod_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lookup
        {
            public static string? Nothing() => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_ExpressionAndBlockLambda_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Factories
        {
            public static System.Func<string?> Expression() => () => {|FP0303:null|};

            public static System.Func<int, string?> Block() => key =>
            {
                if (key < 0)
                    return {|FP0303:default|};

                return "found";
            };
        }
        """);

    [TestMethod]
    public Task FP0303_LambdaInFieldInitializer_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Factories
        {
            public static readonly System.Func<string?> Empty = () => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_LocalFunction_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lookup
        {
            public static int Length(string text)
            {
                return Trimmed(text)?.Length ?? 0;

                static string? Trimmed(string value)
                {
                    if (value.Length == 0)
                        return {|FP0303:null|};

                    return value.Trim();
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0303_NullAndDefaultForNullableValueType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Numbers
        {
            public static int? None() => {|FP0303:null|};

            public static int? Zero() => {|FP0303:default|};

            public static int? Typed() => {|FP0303:default(int?)|};
        }
        """);

    [TestMethod]
    public Task FP0303_DefaultOfValueTypeConvertedToNullableOrReference_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Numbers
        {
            public static int? Lifted() => {|FP0303:default(int)|};

            public static object Boxed() => {|FP0303:default(int)|};
        }
        """);

    [TestMethod]
    public Task FP0303_DefaultForNonNullableValueType_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Numbers
        {
            public static int Zero() => default;

            public static int Typed() => default(int);

            public static (string? Name, int Count) Pair() => (null, 0);

            public static System.DateTime Epoch(bool reset) => reset ? default : System.DateTime.UnixEpoch;

            public static System.Func<int> Factory() => () => default;
        }
        """);

    [TestMethod]
    public Task FP0303_AsyncTaskOfReferenceTypeReturnsNull_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Loader
        {
            public static async System.Threading.Tasks.Task<string?> Load(System.Threading.Tasks.Task<int> pending)
            {
                if (await pending < 0)
                    return {|FP0303:null|};

                return "loaded";
            }
        }
        """);

    [TestMethod]
    public Task FP0303_AsyncTaskOfIntReturnsDefault_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0303_NullInConditionalBranches_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Choice
        {
            public static string? First(bool flag) => flag ? {|FP0303:null|} : "value";

            public static string? Second(bool flag)
            {
                return flag ? "value" : {|FP0303:default|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_NullInSwitchExpressionArm_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Days
        {
            public static string? Name(int day) => day switch
            {
                0 => "Sunday",
                1 => {|FP0303:default|},
                _ => {|FP0303:null|}
            };
        }
        """);

    [TestMethod]
    public Task FP0303_NullOnRightSideOfCoalesce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Fallback
        {
            public static string? Pick(string? first, bool flag) => first ?? (flag ? "second" : {|FP0303:null|});

            public static string? Other(string? first) => first ?? {|FP0303:default|};
        }
        """);

    [TestMethod]
    public Task FP0303_DefaultOfUnconstrainedTypeParameter_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Generic
        {
            public static T Typed<T>() => {|FP0303:default(T)|};

            public static T? Literal<T>() => {|FP0303:default|};

            public static T? Nullable<T>() where T : struct => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_DefaultOfStructConstrainedTypeParameter_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Generic
        {
            public static T Typed<T>() where T : struct => default(T);

            public static T Literal<T>() where T : unmanaged => default;
        }
        """);

    [TestMethod]
    public Task FP0303_NullForgivingNullReportsAtNullLiteral_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Lies
        {
            public static string Never() => {|FP0303:null|}!;

            public static string Also() => {|FP0303:default|}!;
        }
        """);

    [TestMethod]
    public Task FP0303_CastNullReportsAtNullLiteral_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Casts
        {
            public static object? Cast() => (string?){|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_YieldReturnNullForReferenceElement_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Sequences
        {
            public static System.Collections.Generic.IEnumerable<string?> Items()
            {
                yield return "first";
                yield return {|FP0303:null|};
                yield return {|FP0303:default|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_YieldReturnDefaultForValueElement_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0303_PropertyGetterAndOperatorCountAsMethods_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Box
        {
            public string? Label => {|FP0303:null|};

            public string? Tag
            {
                get { return {|FP0303:default|}; }
            }

            public static string? operator +(Box left, Box right) => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_NullPointerCountsAsNull_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static unsafe class Pointers
        {
            public static int* Nowhere() => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_InTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static string? Nothing() => {|FP0303:null|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static string? Nothing() => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_OutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static string? Nothing() => null;

            public static string? Default() => default;

            public static System.Func<string?> Factory() => () => null;

            public static string? Choice(bool flag) => flag ? null : "value";
        }
        """);

    [TestMethod]
    public Task FP0303_NullUsedButNotReturned_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0303_CompilerGeneratedRecordMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed record Person(string Name, string? Nickname);

        [Fprog.FunctionalCore]
        public abstract record Shape
        {
            public sealed record Circle(double Radius) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0303_AnonymousMethod_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Factories
        {
            public static System.Func<string?> Empty() => delegate { return {|FP0303:null|}; };

            public static System.Func<int, object?> Typed() => delegate (int key) { return key < 0 ? {|FP0303:default|} : key; };
        }
        """);

    [TestMethod]
    public Task FP0303_AsyncLambdaAndAsyncLocalFunction_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Loader
        {
            public static System.Func<System.Threading.Tasks.Task<string?>> Lambda() => async () =>
            {
                await System.Threading.Tasks.Task.Yield();
                return {|FP0303:null|};
            };

            public static System.Threading.Tasks.ValueTask<int?> Local()
            {
                return Inner();

                static async System.Threading.Tasks.ValueTask<int?> Inner()
                {
                    await System.Threading.Tasks.Task.Yield();
                    return {|FP0303:default|};
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0303_ExpressionTreeLambda_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Trees
        {
            public static System.Linq.Expressions.Expression<System.Func<string?>> Empty() => () => {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_QuerySelectClauseIsALambda_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Queries
        {
            public static IEnumerable<string?> Blanks(IEnumerable<string> words)
                => from word in words
                   select word.Length == 0 ? {|FP0303:null|} : word;
        }
        """);

    [TestMethod]
    public Task FP0303_IndexerAndConversionOperators_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Box
        {
            public string? this[int index] => {|FP0303:null|};

            public object? this[string key]
            {
                get { return {|FP0303:default|}; }
            }

            public static implicit operator string?(Box box) => {|FP0303:null|};

            public static explicit operator int?(Box box) => {|FP0303:default(int?)|};
        }
        """);

    [TestMethod]
    public Task FP0303_ExtensionBlockMembers_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class TextExtensions
        {
            extension(string text)
            {
                public string? Blank => {|FP0303:null|};

                public string? Head() => text.Length == 0 ? {|FP0303:default|} : text.Substring(0, 1);
            }
        }
        """);

    [TestMethod]
    public Task FP0303_RecordStructInterfaceAndGenericTypeMembers_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public readonly record struct Point(int X, int Y)
        {
            public string? Label() => {|FP0303:null|};
        }

        [Fprog.FunctionalCore]
        public static class Holder
        {
            public interface IShape
            {
                string? Name() => {|FP0303:null|};
            }

            public sealed class Box<T>
            {
                public T? Get() => {|FP0303:default|};

                public TOther? Map<TOther>() where TOther : class => {|FP0303:null|};

                public TKey? Key<TKey>() where TKey : notnull => {|FP0303:default(TKey)|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_PartialMethodAndPartialProperty_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed partial class Parts
        {
            public partial string? Name();

            public partial string? Name() => {|FP0303:null|};

            public partial string? Label { get; }

            public partial string? Label => {|FP0303:default|};
        }
        """);

    [TestMethod]
    public Task FP0303_NestedConditionalsSwitchesAndParentheses_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static string? Deep(bool a, bool b, int c)
                => a
                    ? (b ? ({|FP0303:null|}) : "x")
                    : c switch
                    {
                        0 => b ? "y" : {|FP0303:default|},
                        1 => (string?)"z" ?? {|FP0303:null|},
                        _ => "w"
                    };

            public static string? Parenthesized() { return ({|FP0303:null|}); }

            public static string? Trimmed(string? text) => text?.Trim() ?? {|FP0303:null|};
        }
        """);

    [TestMethod]
    public Task FP0303_SpanOfQualifiedAliasedAndCastDefaults_Reports() => VerifyAsync("""
        using Text = System.String;

        [Fprog.FunctionalCore]
        public static class Spans
        {
            public static string? Global() => {|FP0303:default(global::System.String)|};

            public static string? Alias() => {|FP0303:default(Text)|};

            public static System.Collections.Generic.List<int>? Generic() => {|FP0303:default(System.Collections.Generic.List<int>?)|};

            public static string? CastDefault() => (string?){|FP0303:default|};

            public static string? Commented()
            {
                return /* nothing */ {|FP0303:default /* type */ (
                    string)|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_FunctionPointerAndVoidPointer_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static unsafe class Pointers
        {
            public static delegate*<int> NoFunction() => {|FP0303:null|};

            public static void* NoAddress() => {|FP0303:default|};
        }
        """);

    [TestMethod]
    public Task FP0303_AsyncIteratorAndNullableValueElements_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Streams
        {
            public static async System.Collections.Generic.IAsyncEnumerable<string?> Names()
            {
                await System.Threading.Tasks.Task.Yield();
                yield return {|FP0303:null|};
            }

            public static System.Collections.Generic.IEnumerable<int?> Numbers()
            {
                yield return {|FP0303:default|};
            }

            public static System.Collections.IEnumerable Untyped()
            {
                yield return {|FP0303:null|};
            }

            public static async System.Threading.Tasks.Task<int?> Missing(System.Threading.Tasks.Task<int> pending)
            {
                await pending;
                return {|FP0303:null|};
            }
        }
        """);

    [TestMethod]
    public Task FP0303_DynamicAndNullableDisabledContext_Reports() => VerifyAsync("""
        #nullable disable
        [Fprog.FunctionalCore]
        public static class Legacy
        {
            public static string Oblivious() => {|FP0303:null|};

            public static dynamic Anything() => {|FP0303:default|};
        }
        """);

    [TestMethod]
    public Task FP0303_NestedLambdasReportOnlyInnermostNull_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Curried
        {
            public static System.Func<System.Func<System.Func<string?>>> Make() => () => () => () => {|FP0303:null|};

            public static System.Func<string?> Pick(int day) => day switch
            {
                0 => () => {|FP0303:default|},
                _ => static () => "x"
            };
        }
        """);

    [TestMethod]
    public Task FP0303_LambdasInInitializersAndPrimaryConstructorBase_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0303_CoreStructAndCoreNestedInShell_Reports() => VerifyAsync("""
        public static class Shell
        {
            public static string? Outside() => null;

            [Fprog.FunctionalCore]
            public readonly struct Inside
            {
                public static string? Nothing() => {|FP0303:null|};
            }

            public static class Sibling
            {
                public static string? Outside() => default;
            }
        }
        """);

    [TestMethod]
    public Task FP0303_ValueTypesWhoseDefaultHoldsNothing_NoReport() => VerifyAsync("""
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

            public static (string? Name, string? Nick) Names() => default;

            public static System.Threading.Tasks.ValueTask<int> Ready() => default;

            public static System.Func<Color> Factory() => () => default(Color);

            public static T Struct<T>() where T : struct, System.Enum => default;
        }
        """);

    [TestMethod]
    public Task FP0303_NullHeldInLocalsInitializersAndArguments_NoReport() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public sealed record Person(string Name, string? Nick = null)
        {
            public string? Alias { get; init; } = null;

            public Person Forget() => this with { Nick = null, Alias = default };

            public static Person Anonymous() => new("anonymous") { Alias = null };

            public static ImmutableArray<string?> Slots() => [null, default, "x"];

            public static string Describe(IEnumerable<string> names, string? fallback = null)
            {
                string? found = null;
                foreach (var name in names)
                    found ??= name;

                return found ?? fallback ?? "";
            }

            public static string? First(IEnumerable<string> names) => names.FirstOrDefault();

            public static string? Cast(object value) => value as string;

            public static string Label(string? text) => text is null or "" ? "none" : $"{text}{null}";

            public static System.Func<string?, bool> IsMissing() => text => text == default;

            public static System.Action Nothing() => () => { return; };
        }
        """);

    [TestMethod]
    public Task FP0303_GeneratedCode_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.FunctionalCore]
        public static class Generated
        {
            public static string? Nothing() => null;

            public static string? Default() => default;
        }
        """);

    [TestMethod]
    public Task FP0303_MembersWithoutBodies_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract partial class Shapes
        {
            public abstract string? Name();

            public abstract string? Label { get; }

            [System.Runtime.InteropServices.DllImport("native")]
            public static extern string? Native();

            partial void Hook();

            public delegate string? Factory();

            public interface IShape
            {
                string? Name();

                static abstract string? Create();
            }
        }
        """);

    [TestMethod]
    public Task FP0303_NullAssignedInReturnedExpression_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Assignments
        {
            public static string? Coalesce(string? text) => text ??= {|FP0303:null|};

            public static string? Simple(string? text)
            {
                return text = {|FP0303:default|};
            }

            public static string Kept(string? text) => text ??= "none";
        }
        """);

    [TestMethod]
    public Task FP0303_NewNullableValueTypeIsNotALiteral_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Probe
        {
            public static int? Created() => new int?();
        }
        """);

    [TestMethod]
    public Task FP0303_ConstantHoldingNullIsNotALiteral_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Probe
        {
            private const string? Nothing = null;

            public static string? Named() => Nothing;
        }
        """);

    [TestMethod]
    public Task FP0303_ReturnsInsideStatementsAndConstructorLocalFunctions_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Statements : Statements.IShape
        {
            public interface IShape
            {
                string? Name { get; }

                string? Describe();
            }

            static Statements()
            {
                _ = Local();

                static string? Local() => {|FP0303:null|};
            }

            string? IShape.Name => {|FP0303:null|};

            string? IShape.Describe() => {|FP0303:default|};

            public static string? Day(int day)
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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0303", source, testContext.CancellationToken);
}
