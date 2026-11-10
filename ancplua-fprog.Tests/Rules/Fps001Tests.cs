using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fps001Tests(TestContext testContext)
{
    private const string Shapes = """
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        """;

    private static DiagnosticResult Suppressed => DiagnosticResult.CompilerWarning("CS8509").WithLocation(0).WithIsSuppressed(true);

    private static DiagnosticResult NotSuppressed => DiagnosticResult.CompilerWarning("CS8509").WithLocation(0).WithIsSuppressed(false);

    [TestMethod]
    public Task FPS001_TypePatternForEveryCase_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_DeclarationAndPositionalPatterns_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static double Area(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle(var radius) => 3.14 * radius * radius,
                Shape.Square square => square.Side * square.Side
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_PropertyPatternWithVarAndEmptyBraces_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static double Size(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle { Radius: var radius } => radius,
                Shape.Square { } => 1.0
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_CasesCombinedWithOr_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static bool IsShape(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle or Shape.Square => true
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_UnionInsideCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Domain
        {
            [Fprog.Union]
            public abstract record Shape
            {
                private Shape() { }

                public sealed record Circle(double Radius) : Shape;

                public sealed record Square(double Side) : Shape;
            }

            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_UnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        """ + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_GenericUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }

        public static class Results
        {
            public static string Describe(Result<int> result) => result {|#0:switch|}
            {
                Result<int>.Ok(var value) => value.ToString(),
                Result<int>.Error(var message) => message
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_TypeParameterConstrainedToUnion_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name<TShape>(TShape shape)
                where TShape : Shape
                => shape {|#0:switch|}
                {
                    Shape.Circle => "circle",
                    Shape.Square => "square"
                };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_UnionClassNotRecord_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Token
        {
            private Token() { }

            public sealed class Number : Token { }

            public sealed class Word : Token { }
        }

        public static class Lexer
        {
            public static int Kind(Token token) => token {|#0:switch|}
            {
                Token.Number => 1,
                Token.Word => 2
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_OnlyTheCompleteOfTwoSwitches_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };

            public static string Round(Shape shape) => shape {|#1:switch|}
            {
                Shape.Circle => "round"
            };
        }
        """, Suppressed, DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(false));

    [TestMethod]
    public Task FPS001_NonNullableValueWithNullableEnabled_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableValueWithNullArm_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape) => shape {|#0:switch|}
            {
                null => "none",
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableValueWithoutNullArmStaysUnsuppressed_NoReport() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_MissingCase_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_CaseOnlyInArmWithWhenClause_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle circle when circle.Radius > 0 => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_CaseOnlyWithRefutableSubpattern_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle { Radius: > 0 } => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_CaseSplitBySubpatternsIsNotOwnArm_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Inner
        {
            private Inner() { }

            public sealed record A : Inner;

            public sealed record B : Inner;
        }

        [Fprog.Union]
        public abstract record Outer
        {
            private Outer() { }

            public sealed record Ok(Inner Value) : Outer;

            public sealed record Failed(string Message) : Outer;
        }

        public static class Reader
        {
            public static int Code(Outer outer) => outer {|#0:switch|}
            {
                Outer.Ok(Inner.A) => 1,
                Outer.Ok(Inner.B) => 2,
                Outer.Failed => 3
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_HierarchyWithoutUnionAttribute_NoReport() => VerifyAsync("""
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_NonUnionValueNextToUnion_NoReport() => VerifyAsync(Shapes + """
        public static class Days
        {
            public static string Name(int day) => day {|#0:switch|}
            {
                0 => "Sunday",
                1 => "Monday"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_CompleteSwitchHasNoCompilerWarning_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape switch
            {
                Shape.Circle => "circle",
                Shape.Square => "square",
                _ => "other"
            };
        }
        """);

    private Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => FpAnalyzerTestHost.VerifySuppressionAsync("CS8509", source, testContext.CancellationToken, expected);
}
