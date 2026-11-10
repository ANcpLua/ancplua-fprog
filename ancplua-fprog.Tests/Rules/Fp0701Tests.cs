using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0701Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0701_LocalAssignedAfterLambda_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run()
            {
                var count = 1;
                System.Func<int> read = () => {|FP0701:count|} + count;
                count = 2;
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_LocalAssignedBeforeLambda_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run()
            {
                var count = 1;
                count = 2;
                System.Func<int> read = () => {|FP0701:count|};
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_IncrementInsideLambda_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static System.Func<int> Next()
            {
                var count = 0;
                return () => ++{|FP0701:count|};
            }
        }
        """);

    [TestMethod]
    public Task FP0701_PostfixDecrementOutside_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run()
            {
                var count = 5;
                System.Func<int> read = () => {|FP0701:count|};
                count--;
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_CompoundAssignmentAndCoalesceAssignment_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run(string? name)
            {
                var total = 1;
                System.Func<int> read = () => {|FP0701:total|} + {|FP0701:name|}!.Length;
                total += 3;
                name ??= "x";
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_RefAndOutArguments_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run(string text)
            {
                var a = 1;
                var b = 2;
                System.Func<int> read = () => {|FP0701:a|} + {|FP0701:b|};
                Bump(ref a);
                int.TryParse(text, out b);
                return read();
            }

            private static int Bump(ref int value) => value + 1;
        }
        """);

    [TestMethod]
    public Task FP0701_DeconstructionIntoExistingLocals_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pairs
        {
            public static int Run()
            {
                var a = 1;
                var b = 2;
                var c = 3;
                System.Func<int> read = () => {|FP0701:a|} + {|FP0701:b|} + c;
                (a, (b, _)) = (4, (5, 6));
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ParameterAssigned_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Clamp
        {
            public static System.Func<int> Of(int value)
            {
                if (value < 0)
                    value = 0;
                return () => {|FP0701:value|};
            }
        }
        """);

    [TestMethod]
    public Task FP0701_LocalFunctionCapturesMutatedLocal_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run()
            {
                var count = 0;
                count = 3;
                return Twice();

                int Twice() => {|FP0701:count|} * 2 + count;
            }
        }
        """);

    [TestMethod]
    public Task FP0701_AnonymousMethodDelegate_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Counter
        {
            public static int Run()
            {
                var count = 0;
                System.Func<int> read = delegate { return {|FP0701:count|}; };
                count = 1;
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ForLoopVariableCapturedInBody_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Loops
        {
            public static int Run()
            {
                var sum = 0;
                for (var i = 0; i < 3; i++)
                {
                    System.Func<int> read = () => {|FP0701:i|};
                    sum = sum + read();
                }

                return sum;
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ForeachVariableNeverAssigned_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Loops
        {
            public static System.Collections.Immutable.ImmutableArray<System.Func<int>> Run(System.Collections.Immutable.ImmutableArray<int> items)
            {
                var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<System.Func<int>>();
                foreach (var item in items)
                    builder.Add(() => item);
                return builder.ToImmutable();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_LocalWithoutInitializerAssignedOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Late
        {
            public static int Run(bool flag)
            {
                int value;
                value = flag ? 1 : 2;
                System.Func<int> read = () => {|FP0701:value|};
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_NestedClosureReportsOuterAndInnerOnceEach_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static int Run()
            {
                var count = 0;
                System.Func<int> outer = () =>
                {
                    var first = {|FP0701:count|};
                    System.Func<int> inner = () => {|FP0701:count|} + count;
                    return first + inner();
                };
                count = 1;
                return outer();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_NestedClosureSameFirstUseReportedOnce_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static int Run()
            {
                var count = 0;
                System.Func<System.Func<int>> outer = () => () => {|FP0701:count|};
                count = 1;
                return outer()();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_InnerClosureCapturesLocalOfOuterClosure_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Nested
        {
            public static System.Func<int> Run() => () =>
            {
                var local = 0;
                System.Func<int> inner = () => {|FP0701:local|};
                local = 2;
                return inner();
            };
        }
        """);

    [TestMethod]
    public Task FP0701_StructFieldAssignedThroughLocal_Reports() => VerifyAsync("""
        public struct Point
        {
            public int X;
        }

        [Fprog.FunctionalCore]
        public static class Points
        {
            public static int Run()
            {
                var point = new Point();
                System.Func<int> read = () => {|FP0701:point|}.X;
                point.X = 3;
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ClosureInTypeNestedInCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public static class Inner
            {
                public static int Run()
                {
                    var count = 0;
                    count++;
                    System.Func<int> read = () => {|FP0701:count|};
                    return read();
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ClosureUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        public static class Anywhere
        {
            public static int Run()
            {
                var count = 0;
                count++;
                System.Func<int> read = () => {|FP0701:count|};
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_QueryClauseCapturesMutatedLocal_Reports() => VerifyAsync("""
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Queries
        {
            public static int Run(int[] items)
            {
                var limit = 1;
                limit = 2;
                return (from item in items where item > {|FP0701:limit|} select item).Count();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_SameCodeOutsideCore_NoReport() => VerifyAsync("""
        public static class Shell
        {
            public static int Run()
            {
                var count = 0;
                System.Func<int> read = () => count++;
                count = 4;
                return read();
            }
        }
        """);

    [TestMethod]
    public Task FP0701_CapturedButNeverReassigned_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pure
        {
            public static System.Func<int, int> AddTo(int offset)
            {
                var scale = 2;
                (int a, int b) = (1, 2);
                int.TryParse("3", out var parsed);
                return x => (x + offset + a + b + parsed) * scale;
            }
        }
        """);

    [TestMethod]
    public Task FP0701_ClosureMutatesOnlyItsOwnLocalsAndParameters_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pure
        {
            public static int Run()
            {
                System.Func<int, int> sum = n =>
                {
                    var total = 0;
                    for (var i = 0; i < n; i++)
                        total += i;
                    n = 0;
                    return total + n;
                };

                return Local(3);

                int Local(int x)
                {
                    x++;
                    return sum(x);
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0701_MutatedLocalNotCaptured_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pure
        {
            public static int Run()
            {
                var count = 0;
                count++;
                System.Func<int> read = () => nameof(count).Length;
                return read() + count;
            }
        }
        """);

    [TestMethod]
    public Task FP0701_StaticLambdaAndStaticLocalFunction_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Pure
        {
            public static int Run()
            {
                var count = 0;
                count++;
                System.Func<int, int> twice = static x => x * 2;
                return twice(count) + Square(count);

                static int Square(int x) => x * x;
            }
        }
        """);

    [TestMethod]
    public Task FP0701_CapturedThis_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public sealed class Holder
        {
            private readonly int _value = 1;

            public System.Func<int> Reader() => () => _value + this._value;
        }
        """);

    [TestMethod]
    public Task FP0701_GeneratedCodeAndRecordMembers_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Outer
        {
            public sealed record Pair(int Left, int Right);

            [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
            public static class Generated
            {
                public static int Run()
                {
                    var count = 0;
                    count++;
                    System.Func<int> read = () => count;
                    return read();
                }
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0701", source, testContext.CancellationToken);
}
