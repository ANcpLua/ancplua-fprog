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

    [TestMethod]
    public Task FP0502_MissingCaseInInitializersAndLambdas_Reports() => VerifyAsync("using System;\n\n" + Shapes + """
        public static class Area
        {
            private static readonly Func<Shape, double> Field = shape => shape {|FP0502:switch|}
            {
                Shape.Circle c => c.Radius
            };

            public static Func<Shape, double> Property { get; } = static shape => shape {|FP0502:switch|}
            {
                Shape.Square s => s.Side
            };

            public static double Local(Shape shape)
            {
                return Inner(shape) + Field(shape);

                static double Inner(Shape value) => value {|FP0502:switch|}
                {
                    Shape.Triangle t => t.A
                };
            }
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInMembersOfEveryKind_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Threading.Tasks;

        """ + Shapes + """
        public sealed partial class Holder(Shape shape)
        {
            public Shape Value { get; } = shape;

            public double Get
            {
                get
                {
                    return Value {|FP0502:switch|}
                    {
                        Shape.Circle c => c.Radius
                    };
                }
            }

            public double this[int index] => Value {|FP0502:switch|}
            {
                Shape.Square s => s.Side + index
            };

            public static double operator +(Holder holder, double extra) => holder.Value {|FP0502:switch|}
            {
                Shape.Triangle t => t.A + extra
            };

            public static explicit operator double(Holder holder) => holder.Value {|FP0502:switch|}
            {
                Shape.Circle c => c.Radius
            };

            public async Task<double> LaterAsync()
            {
                await Task.Yield();
                return Value {|FP0502:switch|}
                {
                    Shape.Square s => s.Side
                };
            }

            public IEnumerable<double> Many()
            {
                yield return Value {|FP0502:switch|}
                {
                    Shape.Triangle t => t.A
                };
            }

            public partial double Size(double factor);

            public partial double Size(double factor) => Value {|FP0502:switch|}
            {
                Shape.Circle c => c.Radius * factor
            };

            public partial double Area { get; }

            public partial double Area => Value {|FP0502:switch|}
            {
                Shape.Square s => s.Side * s.Side
            };

            ~Holder()
            {
                _ = Value {|FP0502:switch|}
                {
                    Shape.Triangle t => t.A
                };
            }
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInConstructorsAndConstructorInitializers_Reports() => VerifyAsync(Shapes + """
        public abstract class Base(double size)
        {
            public double Size { get; } = size;
        }

        public sealed class Derived : Base
        {
            public Derived(Shape shape)
                : base(shape {|FP0502:switch|} { Shape.Circle c => c.Radius })
            {
            }
        }

        public sealed class Primary(Shape shape) : Base(shape {|FP0502:switch|} { Shape.Square s => s.Side });

        public abstract record BaseRecord(double Size);

        public sealed record PrimaryRecord(Shape Value) : BaseRecord(Value {|FP0502:switch|} { Shape.Triangle t => t.A });

        public sealed class Chained
        {
            public Chained(double size) => Size = size;

            public Chained(Shape shape)
                : this(shape {|FP0502:switch|} { Shape.Circle c => c.Radius })
            {
            }

            public double Size { get; }
        }

        public static class Statics
        {
            public static readonly double Unit;

            static Statics()
            {
                Unit = Make() {|FP0502:switch|} { Shape.Square s => s.Side };
            }

            private static Shape Make() => new Shape.Square(1);
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInStructRecordStructInterfaceAndNestedGenericType_Reports() => VerifyAsync(Shapes + """
        public struct Measure(Shape shape)
        {
            public readonly double Size => shape {|FP0502:switch|} { Shape.Circle c => c.Radius };
        }

        public readonly record struct Pair(Shape Value)
        {
            public double Size => Value {|FP0502:switch|} { Shape.Square s => s.Side };
        }

        public interface IMeasured
        {
            Shape Value { get; }

            double Size => Value {|FP0502:switch|} { Shape.Triangle t => t.A };

            static double Of(Shape shape) => shape {|FP0502:switch|} { Shape.Circle c => c.Radius };
        }

        public static class Outer<T>
        {
            public sealed class Inner<TOther>
            {
                public static double Size(Shape shape) => shape {|FP0502:switch|} { Shape.Square s => s.Side };
            }
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInExtensionMembers_Reports() => VerifyAsync(Shapes + """
        public static class ShapeExtensions
        {
            extension(Shape shape)
            {
                public double Size => shape {|FP0502:switch|} { Shape.Circle c => c.Radius };

                public double Scaled(double factor) => shape {|FP0502:switch|} { Shape.Square s => s.Side * factor };
            }

            extension(Shape)
            {
                public static double Unit(Shape shape) => shape {|FP0502:switch|} { Shape.Triangle t => t.A };
            }
        }
        """);

    [TestMethod]
    public Task FP0502_MissingCaseInsideOtherExpressions_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;

        """ + Shapes + """
        public sealed record Report(string Text, double Size);

        public static class Area
        {
            public static string Text(Shape shape) => $"size {(shape {|FP0502:switch|} { Shape.Circle c => c.Radius })}";

            public static IEnumerable<double> Sizes(IEnumerable<Shape> shapes)
                => from shape in shapes
                   let size = shape {|FP0502:switch|} { Shape.Square s => s.Side }
                   select size;

            public static Report Make(Shape shape) => new("x", 0) { Size = shape {|FP0502:switch|} { Shape.Triangle t => t.A } };

            public static Report Change(Report report, Shape shape) => report with { Size = shape {|FP0502:switch|} { Shape.Circle c => c.Radius } };

            public static double[] All(Shape shape) => [shape {|FP0502:switch|} { Shape.Square s => s.Side }];

            public static double Nested(Shape outer, Shape inner) => (inner {|FP0502:switch|} { Shape.Circle => outer, Shape.Square => outer }) switch
            {
                Shape.Circle c when (outer {|FP0502:switch|} { Shape.Circle => true }) => c.Radius,
                Shape.Circle c => c.Radius,
                Shape.Square s => s.Side,
                Shape.Triangle t => t.A
            };
        }
        """);

    [TestMethod]
    public Task FP0502_SpanAfterThisConditionalAccessCastAndComments_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public sealed class Holder(Shape shape)
        {
            public Shape Value { get; } = shape;

            public double Own => this.Value {|FP0502:switch|} { Shape.Circle c => c.Radius };

            public static double Maybe(Holder? holder) => holder?.Value /* comment */
                {|FP0502:switch|}
                {
                    Shape.Circle c => c.Radius,
                    null => 0.0
                };

            public static double Cast(object value) => ((Shape)value) {|FP0502:switch|} { Shape.Square s => s.Side };

            public static double Parenthesized(Shape value) => (value) // trailing
                {|FP0502:switch|} { Shape.Triangle t => t.A };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseOnlyInArmWithWhenTrue_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape {|FP0502:switch|}
            {
                Shape.Circle c when true => c.Radius,
                Shape.Square s => s.Side,
                Shape.Triangle t => t.A
            };
        }
        """);

    [TestMethod]
    public Task FP0502_PrivateCaseSeenFromOutside_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Token
        {
            private Token() { }

            public sealed record Word(string Text) : Token;

            private sealed record Secret(int Code) : Token;

            public static Token Hidden(int code) => new Secret(code);
        }

        public static class Tokens
        {
            public static int Length(Token token) => token {|FP0502:switch|}
            {
                Token.Word w => w.Text.Length
            };
        }
        """);

    [TestMethod]
    public Task FP0502_PrivateCaseNamedInsideUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Token
        {
            private Token() { }

            public sealed record Word(string Text) : Token;

            private sealed record Secret(int Code) : Token;

            public int Length => this switch
            {
                Word w => w.Text.Length,
                Secret s => s.Code
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CasesNamedThroughAliasDiscardAndParentheses_NoReport() => VerifyAsync("using Round = Shape.Circle;\n\n" + Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Round _ => 1.0,
                (Shape.Square) => 2.0,
                global::Shape.Triangle { } => 3.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CasesNamedThroughAndPatterns_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle and var c => 1.0,
                { } and Shape.Square => 2.0,
                Shape.Triangle and { A: > 0 } => 3.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_ArmNamesCaseForOtherTypeArgument_Reports() => VerifyAsync(Results + """
        public static class Describe
        {
            public static string Open<T>(Result<T> result) => result {|FP0502:switch|}
            {
                Result<int>.Ok ok => ok.Value.ToString(),
                Result<T>.Error error => error.Message
            };
        }
        """);

    [TestMethod]
    public Task FP0502_CaseThatIsItselfAUnionHandledOnlyThroughItsCases_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Node
        {
            private Node() { }

            public sealed record Leaf(int Value) : Node;

            [Fprog.Union]
            public abstract record Branch : Node
            {
                private Branch() { }

                public sealed record Pair(Node Left, Node Right) : Branch;

                public sealed record Many(Node First) : Branch;
            }
        }

        public static class Nodes
        {
            public static int Count(Node node) => node {|FP0502:switch|}
            {
                Node.Leaf => 1,
                Node.Branch.Pair => 2,
                Node.Branch.Many => 3
            };

            public static int Kind(Node node) => node switch
            {
                Node.Leaf => 1,
                Node.Branch => 2
            };

            public static int Inner(Node.Branch branch) => branch switch
            {
                Node.Branch.Pair => 1,
                Node.Branch.Many => 2
            };
        }
        """);

    [TestMethod]
    public Task FP0502_UnionWithoutCases_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Never
        {
            private Never() { }
        }

        public static class Absurd
        {
            public static int Of(Never never) => never switch
            {
                null => 0
            };

            public static int Empty(Never never) => never switch { };
        }
        """);

    [TestMethod]
    public Task FP0502_ClassUnionWithCasesOutsideIt_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Option
        {
        }

        public sealed class Some(int value) : Option
        {
            public int Value { get; } = value;
        }

        public sealed class None : Option
        {
        }

        public static class Options
        {
            public static int Get(Option option) => option {|FP0502:switch|}
            {
                Some some => some.Value
            };

            public static int All(Option option) => option switch
            {
                Some some => some.Value,
                None => 0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_UnionNestedInGenericTypeMissingCase_Reports() => VerifyAsync("""
        public static class Outer<T>
        {
            [Fprog.Union]
            public abstract record Result
            {
                private Result() { }

                public sealed record Ok(T Value) : Result;

                public sealed record Error(string Message) : Result;
            }

            public static string Inside(Result result) => result switch
            {
                Result.Ok => "ok",
                Result.Error error => error.Message
            };
        }

        public static class Use
        {
            public static string Outside(Outer<int>.Result result) => result switch
            {
                Outer<int>.Result.Ok ok => ok.Value.ToString(),
                Outer<int>.Result.Error error => error.Message
            };

            public static string Missing(Outer<string>.Result result) => result {|FP0502:switch|}
            {
                Outer<string>.Result.Error error => error.Message
            };
        }
        """);

    [TestMethod]
    public Task FP0502_TypeArgumentsDifferOnlyInAnnotationsOrTupleNames_NoReport() => VerifyAsync("#nullable enable\n" + Results + """
        public static class Describe
        {
            public static string Loose(Result<string?> result) => result switch
            {
                Result<string>.Ok ok => ok.Value,
                Result<string>.Error error => error.Message
            };

            public static int Tuple(Result<(int A, int B)> result) => result switch
            {
                Result<(int, int)>.Ok ok => ok.Value.Item1,
                Result<(int X, int Y)>.Error => 0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_TypeParameterConstrainedThroughAnotherTypeParameter_Reports() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Of<TOuter, TInner>(TOuter shape)
                where TOuter : TInner
                where TInner : Shape
                => shape {|FP0502:switch|}
                {
                    Shape.Circle c => c.Radius
                };
        }
        """);

    [TestMethod]
    public Task FP0502_SwitchOverValueThatIsNotAUnion_NoReport() => VerifyAsync(Shapes + """
        public static class Area
        {
            public static double Both(Shape left, Shape right) => (left, right) switch
            {
                (Shape.Circle a, Shape.Circle b) => a.Radius + b.Radius,
                (Shape.Square s, _) => s.Side
            };

            public static double Circle(Shape.Circle circle) => circle switch
            {
                { Radius: > 0 } => circle.Radius
            };

            public static double Boxed(object value) => value switch
            {
                Shape.Circle c => c.Radius
            };

            public static double Dynamic(Shape shape) => ((dynamic)shape) switch
            {
                Shape.Circle c => c.Radius,
                _ => 0.0
            };

            public static double Open<T>(T value) => value switch
            {
                Shape.Circle c => c.Radius,
                _ => 0.0
            };
        }
        """);

    [TestMethod]
    public Task FP0502_UnusualShapes_NoReport() => VerifyAsync("""
        using System;
        using System.Runtime.InteropServices;

        """ + Shapes + """
        public abstract partial class Odd
        {
            public abstract double Size(Shape shape);

            [DllImport("native")]
            public static extern int Native(int value);

            public static unsafe int Pointer(int* value) => *value switch
            {
                0 => 0,
                _ => 1
            };

            public static Func<Shape, Func<Shape, double>> Curried => a => b => (a, b) switch
            {
                _ => b switch
                {
                    Shape.Circle c => a switch
                    {
                        Shape.Circle => c.Radius,
                        Shape.Square => 1.0,
                        Shape.Triangle => 2.0
                    },
                    Shape.Square => 0.0,
                    Shape.Triangle => 0.0
                }
            };

            public static double Checked(Shape shape) => checked(shape switch
            {
                Shape.Circle => 1.0,
                Shape.Square => 2.0,
                Shape.Triangle => 3.0
            });

            partial void Nothing();

            public void Empty() { }
        }
        """);

    [TestMethod]
    public Task FP0502_FunctionalCoreWithCompleteSwitches_NoReport() => VerifyAsync("""
        using System.Collections.Immutable;
        using System.Linq;

        """ + Shapes + """
        [Fprog.FunctionalCore]
        public static class Geometry
        {
            public static double Area(Shape shape) => shape switch
            {
                Shape.Circle { Radius: var r } => 3.14 * r * r,
                Shape.Square(var side) => side * side,
                Shape.Triangle(var a, var b, var c) => Heron(a, b, c)
            };

            public static double Total(ImmutableArray<Shape> shapes) => shapes.Sum(static shape => shape switch
            {
                Shape.Circle c => c.Radius,
                Shape.Square s => s.Side,
                Shape.Triangle t => t.A + t.B + t.C
            });

            public static int CountCircles(ImmutableArray<Shape> shapes)
            {
                var count = 0;
                foreach (var shape in shapes)
                {
                    count += shape switch
                    {
                        Shape.Circle => 1,
                        Shape.Square or Shape.Triangle => 0
                    };
                }

                return count;
            }

            private static double Heron(double a, double b, double c)
            {
                var s = (a + b + c) / 2;
                return System.Math.Sqrt(s * (s - a) * (s - b) * (s - c));
            }
        }
        """);

    [TestMethod]
    public Task FP0502_GadtStyleCasesNamedForTheValueTypeArgument_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Expr<T>
        {
            private Expr() { }

            public sealed record IntLit(int Value) : Expr<int>;

            public sealed record Add(Expr<int> Left, Expr<int> Right) : Expr<int>;

            public sealed record BoolLit(bool Value) : Expr<bool>;
        }

        public static class Eval
        {
            public static int Int(Expr<int> expr) => expr switch
            {
                Expr<int>.IntLit lit => lit.Value,
                Expr<int>.Add add => Int(add.Left) + Int(add.Right)
            };
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0502", source, testContext.CancellationToken);
}
