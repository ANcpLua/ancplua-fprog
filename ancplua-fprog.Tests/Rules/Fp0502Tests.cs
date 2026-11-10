using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0502Tests(TestContext testContext)
{
    private const string Shapes = """
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;

            public sealed record Triangle(double A, double B, double C) : Shape;
        }

        """;

    private const string Results = """
        [Fprog.Union]
        public abstract record Inner
        {
            private Inner() { }

            public sealed record A : Inner;

            public sealed record B : Inner;
        }

        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }

        """;

    [TestMethod]
    public Task FP0502_EveryCaseNamed_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCase_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side
            };
        }
        """);

    [TestMethod]
    public Task FP0502_EmptyArmsWithoutCatchAll_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static int Of(Shape shape) => shape {|FP0502:switch|} { };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyInArmWithWhenClause_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c when c.Radius > 0 => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseWithWhenArmAndArmWithoutWhen_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c when c.Radius <= 0 => 0.0,
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_DiscardCatchAllWithCaseWithoutOwnArm_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                _ => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_DiscardCatchAllAfterEveryCaseNamed_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0,
                _ => -1.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyMatchedByVarPattern_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                var other => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyMatchedByNotNull_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                not null => 0.0,
                null => -1.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyMatchedByUnionTypeOrObject_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape => 0.0
            };

            public static double Boxed(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                object o => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyMatchedByEmptyPropertyPattern_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                { } => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyMatchedByNegatedTypeTest_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                not Shape.Circle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CasesNamedThroughOrPattern_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square or Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseNamedButSubpatternRejectsValues_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle { Radius: > 0 } c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_NestedUnionCasesNamedInPositionalSubpatterns_NoReport() => VerifyAsync(Results + """
        public static class Describe
        {
            public static string Of(Result<Inner> result) => result switch
            {
                Result<Inner>.Ok(Inner.A) => "a",
                Result<Inner>.Ok(Inner.B) => "b",
                Result<Inner>.Error => "error"
            };
        }
        """);

    [TestMethod]
    public Task FP0502_GenericUnionMissingCase_Reports() => VerifyAsync(Results + """
        public static class Describe
        {
            public static string Of(Result<int> result) => result {|FP0502:switch|}
            {
                Result<int>.Ok ok => ok.Value.ToString()
            };

            public static string Open<T>(Result<T> result) => result {|FP0502:switch|}
            {
                Result<T>.Error error => error.Message
            };
        }
        """);

    [TestMethod]
    public Task FP0502_GenericUnionEveryCaseNamed_NoReport() => VerifyAsync(Results + """
        public static class Describe
        {
            public static string Open<T>(Result<T> result) => result switch
            {
                Result<T>.Ok ok => ok.Value?.ToString() ?? "",
                Result<T>.Error error => error.Message
            };
        }
        """);

    [TestMethod]
    public Task FP0502_TypeParameterConstrainedToUnionMissingCase_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of<TShape>(TShape shape) where TShape : Shape => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side
            };
        }
        """);

    [TestMethod]
    public Task FP0502_NestedSwitchExpressionsReportedSeparately_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape outer, Shape inner) => outer switch
            {
                Shape.Circle => inner {|FP0502:switch|}
                {
                    Shape.Square s => s.Side
                },
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_NullableUnionValueMissingCase_Reports() => VerifyAsync(Shapes + """
        #nullable enable
        public static class Area
        {
            public static double Of(Shape? shape) => shape {|FP0502:switch|}
            {
                null => 0.0,
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side
            };

            public static double All(Shape? shape) => shape switch
            {
                null => 0.0,
                Shape.Circle c => 3.14 * c.Radius * c.Radius,
                Shape.Square s => s.Side * s.Side,
                Shape.Triangle => 1.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInCore_Reports() => VerifyAsync(Shapes + """
        [Fprog.FunctionalCore]
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius
            };

            public static class Nested
            {
                public static double Of(Shape shape) => shape {|FP0502:switch|}
                {
                    Shape.Square s => s.Side * s.Side
                };
            }
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseUnderAssemblyLevelCore_Reports() => VerifyAsync("[assembly: Fprog.FunctionalCore]\n\n" + Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius
            };
        }
        """);

    [TestMethod]
    public Task FP0502_SwitchStatementMissingCase_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape)
            {
                switch (shape)
                {
                    case Shape.Circle c:
                        return 3.14 * c.Radius * c.Radius;
                    case Shape.Square s:
                        return s.Side * s.Side;
                }

                return 0.0;
            }
        }
        """);

    [TestMethod]
    public Task FP0502_SwitchOverNonUnionHierarchy_NoReport() => VerifyAsync("""
        public abstract record Animal;

        public sealed record Dog : Animal;

        public sealed record Cat : Animal;

        public static class Sounds
        {
            public static string Of(Animal animal) => animal switch
            {
                Dog => "woof",
                _ => "?"
            };

            public static string Day(int day) => day switch
            {
                0 => "Sunday",
                _ => "other"
            };
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInGeneratedCode_NoReport() => VerifyAsync("// <auto-generated/>\n" + Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c => 3.14 * c.Radius * c.Radius
            };
        }
        """);

    [TestMethod]
    public Task FP0502_RecordWithSynthesizedMembers_NoReport() => VerifyAsync(Shapes + """
        public sealed record Drawing(Shape Shape, string Name);
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0502", source, testContext.CancellationToken);
}
