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

    [TestMethod]
    public Task FPS001_SwitchOnThisInsideUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public string Name => this {|#0:switch|}
            {
                Circle => "circle",
                Square => "square"
            };

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_SwitchInLambdaOfFieldInitializer_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static readonly System.Func<Shape, string> Name = shape => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_SwitchInConstructorAccessorOperatorAndLocalFunction_Reports() => VerifyAsync(Shapes + """
        public sealed class Drawing
        {
            private readonly string _name;

            public Drawing(Shape shape) => _name = shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };

            public string this[Shape shape]
            {
                get => shape {|#1:switch|}
                {
                    Shape.Circle => _name,
                    Shape.Square => "square"
                };
            }

            public static int operator +(Drawing drawing, Shape shape) => drawing._name.Length + shape {|#2:switch|}
            {
                Shape.Circle => 1,
                Shape.Square => 2
            };

            public static int Count(Shape shape)
            {
                return Local(shape);

                static int Local(Shape value) => value {|#3:switch|}
                {
                    Shape.Circle => 1,
                    Shape.Square => 2
                };
            }
        }
        """,
        Suppressed,
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true),
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(2).WithIsSuppressed(true),
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(3).WithIsSuppressed(true));

    [TestMethod]
    public Task FPS001_SwitchInAsyncIteratorQueryAndInterpolation_Reports() => VerifyAsync("""
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;

        """ + Shapes + """
        public static class Geometry
        {
            public static async Task<string> NameAsync(Shape shape)
            {
                await Task.Yield();
                return shape {|#0:switch|}
                {
                    Shape.Circle => "circle",
                    Shape.Square => "square"
                };
            }

            public static IEnumerable<int> Codes(Shape shape)
            {
                yield return shape {|#1:switch|}
                {
                    Shape.Circle => 1,
                    Shape.Square => 2
                };
            }

            public static IEnumerable<string> Names(IEnumerable<Shape> shapes)
                => from shape in shapes
                   select $"{(shape {|#2:switch|} { Shape.Circle => "circle", Shape.Square => "square" })}";
        }
        """,
        Suppressed,
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true),
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(2).WithIsSuppressed(true));

    [TestMethod]
    public Task FPS001_SwitchInRecordPrimaryConstructorBaseArgument_Reports() => VerifyAsync(Shapes + """
        public record Labeled(string Label);

        public sealed record LabeledShape(Shape Shape) : Labeled(Shape {|#0:switch|}
        {
            Shape.Circle => "circle",
            Shape.Square => "square"
        });
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NestedSwitchesOverTwoUnions_Reports() => VerifyAsync("""
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
                Outer.Ok(var inner) => inner {|#1:switch|}
                {
                    Inner.A => 1,
                    Inner.B => 2
                },
                Outer.Failed => 3
            };

            public static int Partial(Outer outer) => outer {|#2:switch|}
            {
                Outer.Ok(var inner) => inner {|#3:switch|}
                {
                    Inner.A => 1
                },
                Outer.Failed => 3
            };
        }
        """,
        Suppressed,
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true),
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(2).WithIsSuppressed(true),
        DiagnosticResult.CompilerWarning("CS8509").WithLocation(3).WithIsSuppressed(false));

    [TestMethod]
    public Task FPS001_AliasGlobalAndParenthesizedCaseNamesWithComment_Reports() => VerifyAsync("""
        using S = Shapes.Shape;

        namespace Shapes
        {
            [Fprog.Union]
            public abstract record Shape
            {
                private Shape() { }

                public sealed record Circle(double Radius) : Shape;

                public sealed record Square(double Side) : Shape;

                public sealed record Triangle(double Base) : Shape;
            }
        }

        public static class Geometry
        {
            public static string Name(S shape) => shape /* the shape */
                {|#0:switch|}
                {
                    S.Circle => "circle",
                    (global::Shapes.Shape.Square) => "square",
                    Shapes.Shape.Triangle and var triangle => triangle.ToString()
                };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_DiscardSubpatternAndGuardedArmBeforeOwnArm_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle { Radius: > 10 } => "big circle",
                Shape.Circle circle when circle.Radius > 5 => "medium circle",
                Shape.Circle(_) => "circle",
                Shape.Square { } square => square.ToString()
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_GenericUnionInGenericMethod_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }

        public static class Results
        {
            public static string Describe<TValue>(Result<TValue> result) => result {|#0:switch|}
            {
                Result<TValue>.Ok(var value) => value?.ToString() ?? "",
                Result<TValue>.Error(var message) => message
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NonNullableValueWithNullArmAndNullableEnabled_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                null => "none",
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableValueWithNullInOrArm_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape) => shape {|#0:switch|}
            {
                Shape.Circle or null => "circle or none",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableValueNarrowedByNullCheck_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape)
            {
                if (shape is null)
                    return "none";

                return shape {|#0:switch|}
                {
                    Shape.Circle => "circle",
                    Shape.Square => "square"
                };
            }
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableComponentMatchedWithVar_Reports() => VerifyAsync("""
        #nullable enable
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Named(string? Name) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Named(var name) => name ?? "anonymous",
                Shape.Square => "square"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_NullableComponentMatchedWithNonNullType_NoReport() => VerifyAsync("""
        #nullable enable
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Named(string? Name) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Named(string name) => name,
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_NullableValueWithGuardedNullArm_NoReport() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape, bool known) => shape {|#0:switch|}
            {
                null when known => "none",
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_TypeParameterWithNullableConstraint_NoReport() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name<TShape>(TShape shape)
                where TShape : Shape?
                => shape {|#0:switch|}
                {
                    Shape.Circle => "circle",
                    Shape.Square => "square"
                };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_ConditionalAccessValueWithNullableEnabled_NoReport() => VerifyAsync("#nullable enable\n" + Shapes + """
        public sealed record Holder(Shape Shape);

        public static class Geometry
        {
            public static string Name(Holder? holder) => holder?.Shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_CaseOutsideUnionNotNamed_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private protected Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        public sealed record Triangle(double Base) : Shape;

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
    public Task FPS001_CaseInOtherPartOfPartialUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial record Shape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;
        }

        public abstract partial record Shape
        {
            public sealed record Square(double Side) : Shape;
        }

        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };

            public static string Round(Shape shape) => shape {|#1:switch|}
            {
                Shape.Circle => "circle"
            };
        }
        """, Suppressed, DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(false));

    [TestMethod]
    public Task FPS001_PrivateCaseTheSwitchCannotName_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public static Shape Hidden { get; } = new Secret();

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;

            private sealed record Secret : Shape;
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
    public Task FPS001_CaseWithItsOwnTypeParameter_NoReport() => VerifyAsync(Shapes.Replace(
        "public sealed record Square(double Side) : Shape;",
        "public sealed record Square(double Side) : Shape;\n\n    public sealed record Many<TItem>(TItem Item) : Shape;") + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square",
                Shape.Many<int> => "many"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_AbstractCaseHandledOnlyThroughItsSubtype_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public abstract record Polygon : Shape;

            public sealed record Square(double Side) : Polygon;
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
    public Task FPS001_TupleOfUnionValues_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static bool Same(Shape left, Shape right) => (left, right) {|#0:switch|}
            {
                (Shape.Circle, Shape.Circle) => true,
                (Shape.Circle, Shape.Square) => false,
                (Shape.Square, Shape.Circle) => false,
                (Shape.Square, Shape.Square) => true
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_ValueTypedAsInterfaceOfUnion_NoReport() => VerifyAsync("""
        public interface IShape { }

        [Fprog.Union]
        public abstract record Shape : IShape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }

        public static class Geometry
        {
            public static string Name(IShape shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_UnionWithoutCases_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Nothing
        {
            private Nothing() { }
        }

        public static class Absurd
        {
            public static int Code(Nothing nothing) => nothing {|#0:switch|} { };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_NullableValueWithNegatedEmptyPropertyPatternArm_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape? shape) => shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square",
                not { } => "none"
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_UnionNestedInGenericOuterType_Reports() => VerifyAsync("""
        public static class Tree<TKey>
        {
            [Fprog.Union]
            public abstract record Node
            {
                private Node() { }

                public sealed record Leaf(TKey Key) : Node;

                public sealed record Branch(Node Left, Node Right) : Node;
            }
        }

        public static class Trees
        {
            public static int Size(Tree<int>.Node node) => node {|#0:switch|}
            {
                Tree<int>.Node.Leaf => 1,
                Tree<int>.Node.Branch(var left, var right) => Size(left) + Size(right)
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_ExplicitlyTypedSubpatterns_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static double Size(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle(double radius) => radius,
                Shape.Square { Side: double side } and not null => side
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_ClassPrimaryConstructorFieldAndStructMember_Reports() => VerifyAsync(Shapes + """
        public sealed class Painter(Shape shape)
        {
            private readonly string _name = shape {|#0:switch|}
            {
                Shape.Circle => "circle",
                Shape.Square => "square"
            };

            public string Name => _name;
        }

        public readonly struct Stamp(Shape shape)
        {
            public int Code { get; } = shape {|#1:switch|}
            {
                Shape.Circle => 1,
                Shape.Square => 2
            };
        }
        """, Suppressed, DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true));

    [TestMethod]
    public Task FPS001_ValueTypedAsCase_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static string Size(Shape.Circle circle) => circle {|#0:switch|}
            {
                { Radius: > 1 } => "big",
                { Radius: <= 1 } => "small"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_NonAbstractUnionWithEveryCaseNamed_Reports() => VerifyAsync("""
        [Fprog.Union]
        public record Shape
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
        """, Suppressed);

    [TestMethod]
    public Task FPS001_RecursiveArmsOnNonNullableValueWithNullableEnabled_Reports() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static double Size(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle(var radius) => radius,
                Shape.Square { } square => square.Side
            };
        }
        """, Suppressed);

    [TestMethod]
    public Task FPS001_ExplicitNotNullTestOnNonNullableValue_NoReport() => VerifyAsync("#nullable enable\n" + Shapes + """
        public static class Geometry
        {
            public static string Name(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle and not null => "circle",
                Shape.Square => "square"
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_SwitchInGeneratedCode_Reports() => VerifyAsync("""
        // <auto-generated/>
        """ + "\n" + Shapes + """
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
    public Task FPS001_SwitchInExtensionBlockAndInterfaceDefaultMember_Reports() => VerifyAsync(Shapes + """
        public static class ShapeExtensions
        {
            extension(Shape shape)
            {
                public string Name => shape {|#0:switch|}
                {
                    Shape.Circle => "circle",
                    Shape.Square => "square"
                };
            }
        }

        public interface IPainter
        {
            int Code(Shape shape) => shape {|#1:switch|}
            {
                Shape.Circle => 1,
                Shape.Square => 2
            };
        }
        """, Suppressed, DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true));

    [TestMethod]
    public Task FPS001_SwitchInLambdaAndLocalFunctionInsideMethod_Reports() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static System.Func<Shape, int> Coder()
            {
                System.Func<Shape, int> code = shape => shape {|#0:switch|}
                {
                    Shape.Circle => 1,
                    Shape.Square => 2
                };
                return Twice;

                int Twice(Shape shape) => code(shape) + shape {|#1:switch|}
                {
                    Shape.Circle or Shape.Square => 0
                };
            }
        }
        """, Suppressed, DiagnosticResult.CompilerWarning("CS8509").WithLocation(1).WithIsSuppressed(true));

    [TestMethod]
    public Task FPS001_OrArmWithRefutableSideIsNotOwnArm_NoReport() => VerifyAsync(Shapes + """
        public static class Geometry
        {
            public static int Code(Shape shape) => shape {|#0:switch|}
            {
                Shape.Circle { Radius: > 0 } or Shape.Square => 1
            };
        }
        """, NotSuppressed);

    [TestMethod]
    public Task FPS001_GenericUnionWithMissingCase_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Option<T>
        {
            private Option() { }

            public sealed record Some(T Value) : Option<T>;

            public sealed record None : Option<T>;
        }

        public static class Options
        {
            public static int Get(Option<int> option) => option {|#0:switch|}
            {
                Option<int>.Some(var value) => value
            };
        }
        """, NotSuppressed);

    private Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => FpAnalyzerTestHost.VerifySuppressionAsync("CS8509", source, testContext.CancellationToken, expected);
}
