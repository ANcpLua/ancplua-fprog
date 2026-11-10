using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0301Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0301_ThrowStatementInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pricing
        {
            public static int Half(int value)
            {
                if (value < 0)
                    {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(value));

                return value / 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowExpressionInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Names
        {
            public static string Required(string? name)
                => name ?? {|FP0301:throw|} new System.ArgumentNullException(nameof(name));
        }
        """);

    [TestMethod]
    public Task FP0301_ExpressionBodiedMemberThatOnlyThrows_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Later
        {
            public static int Pending() => {|FP0301:throw|} new System.NotImplementedException();
        }
        """);

    [TestMethod]
    public Task FP0301_RethrowInCatch_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Retry
        {
            public static int Run(System.Func<int> work)
            {
                try
                {
                    return work();
                }
                catch (System.InvalidOperationException)
                {
                    {|FP0301:throw|};
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInSwitchExpressionArm_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Days
        {
            public static string Name(int day) => day switch
            {
                0 => "Sunday",
                1 => "Monday",
                _ => {|FP0301:throw|} new System.ArgumentOutOfRangeException(nameof(day))
            };
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInLambdaAndLocalFunctionInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Closures
        {
            public static System.Func<int> Lambda() => () => {|FP0301:throw|} new System.InvalidOperationException();

            public static int Local()
            {
                return Fail();

                static int Fail() => {|FP0301:throw|} new System.InvalidOperationException();
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInPropertyAndFieldInitializerInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Settings
        {
            private static readonly string? Raw = System.Environment.GetEnvironmentVariable("FP");

            public static readonly string Value = Raw ?? {|FP0301:throw|} new System.InvalidOperationException();

            public string Name
            {
                get => {|FP0301:throw|} new System.NotSupportedException();
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static void Fail() => {|FP0301:throw|} new System.InvalidOperationException();
        }
        """);

    [TestMethod]
    public Task FP0301_ThrowOutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static void Fail() => throw new System.InvalidOperationException();

            public static int Half(int value)
            {
                if (value < 0)
                    throw new System.ArgumentOutOfRangeException(nameof(value));

                return value / 2;
            }
        }
        """);

    [TestMethod]
    public Task FP0301_ExceptionCreatedAndReturnedInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Errors
        {
            public static System.Exception Describe(string message) => new System.InvalidOperationException(message);

            public static System.Exception? Find(bool failed)
                => failed ? new System.TimeoutException() : null;
        }
        """);

    [TestMethod]
    public Task FP0301_TryCatchWithoutThrowInCore_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Safe
        {
            public static int ParseOrZero(string text)
            {
                try
                {
                    return int.Parse(text);
                }
                catch (System.FormatException)
                {
                    return 0;
                }
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0301", source, testContext.CancellationToken);
}
