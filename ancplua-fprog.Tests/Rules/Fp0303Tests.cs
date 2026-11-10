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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0303", source, testContext.CancellationToken);
}
