using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class UnionSwitchCoverageTests
{
    private const string Circle = "Shapes.Shape.Circle";
    private const string Square = "Shapes.Shape.Square";

    private const string Leaf = "Trees.Tree.Leaf";
    private const string MaybeLeaf = "Trees.Tree.MaybeLeaf";
    private const string Node = "Trees.Tree.Node";
    private const string Counted = "Trees.Tree.Counted";
    private const string Spelled = "Trees.Tree.Spelled";
    private const string Pair = "Trees.Tree.Pair";
    private const string Tagged = "Trees.Tree.Tagged";

    private const string ShapeSource = """
        namespace Shapes
        {
            [Fprog.Union]
            public abstract record Shape
            {
                private Shape() { }

                public sealed record Circle(double Radius) : Shape;

                public sealed record Square(double Side) : Shape;
            }
        }
        """;

    private const string TreeSource = """
        namespace Trees
        {
            [Fprog.Union]
            public abstract record Tree
            {
                private Tree() { }

                public sealed record Leaf(string Name) : Tree;

                public sealed record MaybeLeaf(string? Name) : Tree;

                public sealed record Node(Tree Left, Tree Right) : Tree;

                public sealed record Counted(int? Count, int Total) : Tree;

                public sealed record Spelled(System.Nullable<int> Count) : Tree;

                public sealed record Pair((int Id, string Label) Both) : Tree;

                public sealed record Tagged((string? Tag, int Weight) Mark) : Tree;
            }
        }
        """;

    private const string ResultSource = """
        namespace Results
        {
            [Fprog.Union]
            public abstract record Result<T>
            {
                private Result() { }

                public sealed record Ok(T Value) : Result<T>;

                public sealed record Error(string Message) : Result<T>;

                public int Tag() => this switch { Ok => 1, Error => 2 };
            }
        }
        """;

    private const string OpenResultSource = """
        namespace Open
        {
            [Fprog.Union]
            public abstract record Result<T>;

            public sealed record Pending<T> : Result<T>;

            public sealed record IntOk(int Value) : Result<int>;
        }
        """;

    [TestMethod]
    public void TypePatternForEveryCase_HandlesEveryCase()
    {
        var compilation = TestCompilation.Create(ShapeSwitch("Shape.Circle => 1, Shape.Square => 2"));
        var coverage = Coverage(compilation, "Run");

        Assert.AreEqual(compilation.GetNamedType("Shapes.Shape"), coverage.Union, SymbolEqualityComparer.Default);
        AssertCoverage(coverage, [Circle, Square], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void DeclarationPatternForEveryCase_HandlesEveryCase()
    {
        var coverage = ShapeCoverage("Shape.Circle circle => 1, Shape.Square square => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void PositionalPatternWithVar_HandlesTheCase()
    {
        var coverage = ShapeCoverage("Shape.Circle(var radius) => 1, Shape.Square(_) => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void PropertyPatternWithVar_HandlesTheCase()
    {
        var coverage = ShapeCoverage("Shape.Circle { Radius: var radius } => 1, Shape.Square { Side: _ } => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void EmptyPropertyPatternOnACase_HandlesTheCase()
    {
        var coverage = ShapeCoverage("Shape.Circle { } => 1, Shape.Square { } square => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void MissingCase_IsUnhandled()
    {
        var coverage = ShapeCoverage("Shape.Circle => 1");

        AssertCoverage(coverage, [Circle, Square], [Square], [Square]);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void CaseHandledOnlyUnderWhen_IsUnhandled()
    {
        var coverage = ShapeCoverage("Shape.Circle => 1, Shape.Square square when square.Side > 0 => 2");

        AssertCoverage(coverage, [Circle, Square], [Square], [Square]);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void CatchAllUnderWhen_CoversNothing()
    {
        var coverage = ShapeCoverage("Shape.Circle => 1, _ when shape.ToString().Length > 3 => 2");

        AssertCoverage(coverage, [Circle, Square], [Square], [Square]);
    }

    [TestMethod]
    public void CaseHandledOnlyByARefutablePropertyPattern_IsUnhandled()
    {
        var coverage = ShapeCoverage("Shape.Circle { Radius: > 0 } => 1, Shape.Square => 2");

        AssertCoverage(coverage, [Circle, Square], [Circle], [Circle]);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void DiscardArm_CoversTheOtherCasesButIsNotTheirOwnArm()
    {
        var coverage = ShapeCoverage("Shape.Circle => 1, _ => 2");

        AssertCoverage(coverage, [Circle, Square], [Square], []);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void DiscardArmAfterARefutablePattern_CoversThatCaseToo()
    {
        var coverage = ShapeCoverage("Shape.Circle { Radius: > 0 } => 1, _ => 2");

        AssertCoverage(coverage, [Circle, Square], [Circle, Square], []);
    }

    [TestMethod]
    [DataRow("var other")]
    [DataRow("not null")]
    [DataRow("Shape other")]
    [DataRow("Shape")]
    [DataRow("object")]
    [DataRow("object other")]
    [DataRow("{ }")]
    [DataRow("{ } other")]
    [DataRow("System.IEquatable<Shape>")]
    [DataRow("Shape and not null")]
    public void CatchAllArm_CoversTheOtherCasesButIsNotTheirOwnArm(string catchAll)
    {
        var coverage = ShapeCoverage($"Shape.Circle => 1, {catchAll} => 2");

        AssertCoverage(coverage, [Circle, Square], [Square], []);
    }

    [TestMethod]
    public void OrPatternWithACatchAllSide_IsTheOwnArmOfTheNamedCaseOnly()
    {
        var coverage = ShapeCoverage("Shape.Square or _ => 1");

        AssertCoverage(coverage, [Circle, Square], [Circle], []);
    }

    [TestMethod]
    public void OrPatternNamingTwoCases_HandlesBoth()
    {
        var coverage = ShapeCoverage("Shape.Circle or Shape.Square => 1");

        AssertCoverage(coverage, [Circle, Square], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void OrPatternWithARefutableSide_HandlesOnlyTheOtherSide()
    {
        var coverage = ShapeCoverage("Shape.Circle { Radius: > 0 } or Shape.Square => 1");

        AssertCoverage(coverage, [Circle, Square], [Circle], [Circle]);
    }

    [TestMethod]
    public void AndPatternWithVar_HandlesTheCase()
    {
        var coverage = ShapeCoverage("Shape.Circle and var circle => 1, Shape.Square and { } => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void AndPatternNamingTheCaseOnTheRight_HandlesTheCase()
    {
        var coverage = ShapeCoverage("not null and Shape.Circle => 1, Shape.Square => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void AndPatternWithARefutableSide_DoesNotHandleTheCase()
    {
        var coverage = ShapeCoverage("Shape.Circle and { Radius: > 0 } => 1, Shape.Square => 2");

        AssertCoverage(coverage, [Circle, Square], [Circle], [Circle]);
    }

    [TestMethod]
    public void NegatedTypePattern_HandlesNothing()
    {
        var coverage = ShapeCoverage("not Shape.Circle => 1, Shape.Circle => 2");

        AssertCoverage(coverage, [Circle, Square], [Square], [Square]);
    }

    [TestMethod]
    public void ParenthesizedPatterns_CountLikeTheirContent()
    {
        var coverage = ShapeCoverage("(Shape.Circle) => 1, (Shape.Square square) => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
    }

    [TestMethod]
    public void NullArm_IsNotACase()
    {
        var coverage = ShapeCoverage("null => 0, Shape.Circle => 1, Shape.Square => 2");

        AssertCoverage(coverage, [Circle, Square], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void SwitchedValueThatMayBeNull_HandlesEveryCaseAndIsAnnotated()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                using Shapes;

                public static class Switches
                {
                    public static int NotNull(Shape shape) => shape switch { Shape.Circle => 1, Shape.Square => 2 };

                    public static int Maybe(Shape? shape) => shape switch { Shape.Circle => 1, Shape.Square => 2 };

                    public static int TestedForNull(Shape shape) => shape switch { null => 0, Shape.Circle => 1, Shape.Square => 2 };
                }
            }
            """);

        Assert.IsTrue(Coverage(compilation, "NotNull").HandlesEveryCase);
        Assert.IsTrue(Coverage(compilation, "Maybe").HandlesEveryCase);
        Assert.IsTrue(Coverage(compilation, "TestedForNull").HandlesEveryCase);
        Assert.AreEqual(NullableAnnotation.NotAnnotated, ValueAnnotation(compilation, "NotNull"));
        Assert.AreEqual(NullableAnnotation.Annotated, ValueAnnotation(compilation, "Maybe"));
        Assert.AreEqual(NullableAnnotation.Annotated, ValueAnnotation(compilation, "TestedForNull"));
    }

    [TestMethod]
    public void GenericUnionWithNestedCases_HandlesEveryCase()
    {
        var compilation = TestCompilation.Create(ResultSource + """

            namespace Uses
            {
                using Results;

                public static class Switches
                {
                    public static int Run(Result<int> result) => result switch { Result<int>.Ok => 1, Result<int>.Error => 2 };
                }
            }
            """);
        var coverage = Coverage(compilation, "Run");

        Assert.AreEqual(compilation.GetNamedType("Results.Result`1"), coverage.Union, SymbolEqualityComparer.Default);
        AssertCoverage(coverage, ["Results.Result<T>.Ok", "Results.Result<T>.Error"], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void SwitchInsideTheGenericUnion_HandlesEveryCase()
    {
        var coverage = Coverage(TestCompilation.Create(ResultSource), "Tag");

        AssertCoverage(coverage, ["Results.Result<T>.Ok", "Results.Result<T>.Error"], [], []);
    }

    [TestMethod]
    public void GenericMethod_ArmsNamingTheValueTypeArgument_HandleEveryCase()
    {
        var coverage = ResultCoverage("Result<TValue>.Ok(var value) => 1, Result<TValue>.Error => 2");

        AssertCoverage(coverage, ["Results.Result<T>.Ok", "Results.Result<T>.Error"], [], []);
    }

    [TestMethod]
    public void GenericMethod_ArmNamingAnotherConstruction_DoesNotHandleTheCase()
    {
        var coverage = ResultCoverage("Result<int>.Ok => 1, Result<TValue>.Error => 2");

        AssertCoverage(coverage, ["Results.Result<T>.Ok", "Results.Result<T>.Error"], ["Results.Result<T>.Ok"], ["Results.Result<T>.Ok"]);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void ArmNamingTheTypeArgumentWithOtherTupleElementNames_HandlesTheCase()
    {
        var compilation = TestCompilation.Create(ResultSource + """

            namespace Uses
            {
                using Results;

                public static class Switches
                {
                    public static int Run(Result<(int Id, string Name)> result)
                        => result switch { Result<(int, string)>.Ok => 1, Result<(int Number, string Text)>.Error => 2 };
                }
            }
            """);

        AssertCoverage(Coverage(compilation, "Run"), ["Results.Result<T>.Ok", "Results.Result<T>.Error"], [], []);
    }

    [TestMethod]
    public void CasesWithTheirOwnTypeArguments_AreHandledByTheMatchingConstruction()
    {
        var compilation = TestCompilation.Create(OpenResultSource + """

            namespace Uses
            {
                using Open;

                public static class Switches
                {
                    public static int OfInt(Result<int> result) => result switch { Pending<int> => 1, IntOk => 2 };

                    public static int OfString(Result<string> result) => result switch { Pending<string> => 1 };

                    public static int Generic<TValue>(Result<TValue> result) => result switch { Pending<TValue> => 1, IntOk => 2 };

                    public static int GenericWithInt<TValue>(Result<TValue> result) => result switch { Pending<int> => 1, IntOk => 2 };

                    public static int GenericWithBase<TValue>(Result<TValue> result) => result switch { Result<int> => 1, Pending<TValue> => 2 };
                }
            }
            """);

        AssertCoverage(Coverage(compilation, "OfInt"), ["Open.Pending<T>", "Open.IntOk"], [], []);
        AssertCoverage(Coverage(compilation, "OfString"), ["Open.Pending<T>"], [], []);
        AssertCoverage(Coverage(compilation, "Generic"), ["Open.Pending<T>", "Open.IntOk"], [], []);
        AssertCoverage(Coverage(compilation, "GenericWithInt"), ["Open.Pending<T>", "Open.IntOk"], ["Open.Pending<T>"], ["Open.Pending<T>"]);
        AssertCoverage(Coverage(compilation, "GenericWithBase"), ["Open.Pending<T>", "Open.IntOk"], ["Open.IntOk"], []);
    }

    [TestMethod]
    public void TypeParameterConstrainedToAUnion_IsAUnionValue()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                using Shapes;

                public static class Switches
                {
                    public static int Named<TShape>(TShape shape) where TShape : Shape
                        => shape switch { Shape.Circle => 1, Shape.Square => 2 };

                    public static int CatchAll<TShape>(TShape shape) where TShape : Shape
                        => shape switch { Shape.Circle => 1, TShape other => 2 };
                }
            }
            """);
        var named = Coverage(compilation, "Named");

        Assert.AreEqual(compilation.GetNamedType("Shapes.Shape"), named.Union, SymbolEqualityComparer.Default);
        AssertCoverage(named, [Circle, Square], [], []);
        AssertCoverage(Coverage(compilation, "CatchAll"), [Circle, Square], [Square], []);
    }

    [TestMethod]
    public void InterfaceOfSomeCases_CoversThoseCasesOnly()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public abstract record Shape
                {
                    private Shape() { }

                    public interface IRound { }

                    public sealed record Circle(double Radius) : Shape, IRound;

                    public sealed record Ellipse(double Width, double Height) : Shape, IRound;

                    public sealed record Square(double Side) : Shape;
                }

                public static class Switches
                {
                    public static int Round(Shape shape) => shape switch { Shape.IRound => 1, Shape.Square => 2 };

                    public static int Equatable(Shape shape) => shape switch { System.IEquatable<Shape.Circle> => 1, Shape.Square => 2 };
                }
            }
            """);
        string[] cases = [Circle, "Shapes.Shape.Ellipse", Square];

        AssertCoverage(Coverage(compilation, "Round"), cases, [Circle, "Shapes.Shape.Ellipse"], []);
        AssertCoverage(Coverage(compilation, "Equatable"), cases, [Circle, "Shapes.Shape.Ellipse"], ["Shapes.Shape.Ellipse"]);
    }

    [TestMethod]
    public void PrivateNestedCase_IsACaseOnlyWhereItCanBeNamed()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public abstract record Shape
                {
                    private Shape() { }

                    public sealed record Circle : Shape;

                    private sealed record Hidden : Shape;

                    public static int Inside(Shape shape) => shape switch { Circle => 1 };
                }

                public static class Switches
                {
                    public static int Outside(Shape shape) => shape switch { Shape.Circle => 1 };
                }
            }
            """);
        var outside = Coverage(compilation, "Outside");

        AssertCoverage(outside, [Circle], [], []);
        Assert.IsTrue(outside.HandlesEveryCase);
        AssertCoverage(Coverage(compilation, "Inside"), [Circle, "Shapes.Shape.Hidden"], ["Shapes.Shape.Hidden"], ["Shapes.Shape.Hidden"]);
        AssertNames([Circle, "Shapes.Shape.Hidden"], new UnionModel(compilation).GetCases(compilation.GetNamedType("Shapes.Shape")));
    }

    [TestMethod]
    public void UnionFromAReferencedAssembly_InternalCaseIsNotACaseOfTheSwitch()
    {
        var library = TestCompilation.Create("""
            namespace Lib
            {
                [Fprog.Union]
                public abstract record Shape
                {
                    private Shape() { }

                    public sealed record Circle : Shape;

                    internal sealed record Hidden : Shape;
                }
            }
            """, "Library");
        var compilation = TestCompilation.Create("""
            namespace App
            {
                public static class Switches
                {
                    public static int Run(Lib.Shape shape) => shape switch { Lib.Shape.Circle => 1 };
                }
            }
            """, "App", TestCompilation.ToImageReference(library));
        var coverage = Coverage(compilation, "Run");

        AssertCoverage(coverage, ["Lib.Shape.Circle"], [], []);
        Assert.IsTrue(coverage.HandlesEveryCase);
        AssertNames(["Lib.Shape.Circle", "Lib.Shape.Hidden"], new UnionModel(compilation).GetCases(compilation.GetNamedType("Lib.Shape")));
    }

    [TestMethod]
    public void FileLocalCase_IsACaseOnlyInItsOwnFile()
    {
        var compilation = TestCompilation.Create(
        [
            """
            namespace Shapes
            {
                [Fprog.Union]
                public abstract record Shape
                {
                    public sealed record Circle : Shape;
                }

                file sealed record Local : Shape;

                public static class SameFile
                {
                    public static int Here(Shape shape) => shape switch { Shape.Circle => 1, Local => 2 };
                }
            }
            """,
            """
            namespace Shapes
            {
                public static class OtherFile
                {
                    public static int There(Shape shape) => shape switch { Shape.Circle => 1 };
                }
            }
            """
        ]);

        AssertCoverage(Coverage(compilation, "Here"), [Circle, "Shapes.Local"], [], []);
        AssertCoverage(Coverage(compilation, "There"), [Circle], [], []);
    }

    [TestMethod]
    public void SwitchOverANonUnionValue_ReturnsFalse()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                using Shapes;

                public static class Switches
                {
                    public static int OverObject(object value) => value switch { Shape.Circle => 1, _ => 0 };

                    public static int OverACase(Shape.Circle circle) => circle switch { { Radius: > 0 } => 1, _ => 0 };

                    public static int OverATuple((Shape, Shape) pair) => pair switch { (Shape.Circle, _) => 1, _ => 0 };

                    public static int OverAnyParameter<TValue>(TValue value) => value switch { Shape.Circle => 1, _ => 0 };
                }
            }
            """);
        var model = new UnionModel(compilation);

        foreach (var method in new[] { "OverObject", "OverACase", "OverATuple", "OverAnyParameter" })
        {
            Assert.IsFalse(model.TryGetCoverage(compilation.GetSwitchExpression(method), out var coverage), method);
            AssertCoverage(coverage, [], [], []);
            Assert.IsFalse(coverage.HandlesEveryCase, method);
        }
    }

    [TestMethod]
    public void UnionWithoutCases_DoesNotHandleEveryCase()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public abstract record Shape;

                public static class Switches
                {
                    public static int Run(Shape shape) => shape switch { _ => 1 };
                }
            }
            """);
        var coverage = Coverage(compilation, "Run");

        AssertCoverage(coverage, [], [], []);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    [TestMethod]
    public void PositionalSubpatternWithAType_IsJudgedAgainstTheDeclaredNullability()
    {
        var coverage = TreeCoverage("Tree.Leaf(string name) => 1, Tree.MaybeLeaf(string name) => 2, _ => 0");

        AssertOwnArm(coverage, Leaf);
        AssertNoOwnArm(coverage, MaybeLeaf);
    }

    [TestMethod]
    public void PositionalSubpatternWithAType_InCodeWithoutNullableContext_HandlesTheCase()
    {
        var compilation = TestCompilation.Create("""
            #nullable disable
            namespace Trees
            {
                [Fprog.Union]
                public abstract record Tree
                {
                    private Tree() { }

                    public sealed record Leaf(string Name) : Tree;
                }

                public static class Switches
                {
                    public static int Run(Tree tree) => tree switch { Tree.Leaf(string name) => 1 };
                }
            }
            """);

        AssertCoverage(Coverage(compilation, "Run"), [Leaf], [], []);
    }

    [TestMethod]
    [DataRow("Tree.Leaf(var name)", Leaf)]
    [DataRow("Tree.Leaf(_)", Leaf)]
    [DataRow("Tree.Leaf(string name)", Leaf)]
    [DataRow("Tree.Leaf(object name)", Leaf)]
    [DataRow("Tree.Leaf({ } name)", Leaf)]
    [DataRow("Tree.Leaf(not null)", Leaf)]
    [DataRow("Tree.Leaf(string and var name)", Leaf)]
    [DataRow("Tree.Leaf(\"a\" or _)", Leaf)]
    [DataRow("Tree.Leaf { Name: var name }", Leaf)]
    [DataRow("Tree.Leaf { Name: string name }", Leaf)]
    [DataRow("Tree.Leaf { Name.Length: var length }", Leaf)]
    [DataRow("Tree.MaybeLeaf(var name)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf(_)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf { Name: var name }", MaybeLeaf)]
    [DataRow("Tree.Node(var left, _)", Node)]
    [DataRow("Tree.Node(Tree left, { } right)", Node)]
    [DataRow("Tree.Node(object left, not null)", Node)]
    [DataRow("Tree.Node { Left: var left }", Node)]
    [DataRow("Tree.Counted(var count, int total)", Counted)]
    [DataRow("Tree.Counted(_, object total)", Counted)]
    [DataRow("Tree.Counted(_, System.IComparable total)", Counted)]
    [DataRow("Tree.Counted { Total: { } total }", Counted)]
    [DataRow("Tree.Pair((var id, var label))", Pair)]
    [DataRow("Tree.Pair(var (id, label))", Pair)]
    [DataRow("Tree.Pair((int id, string label))", Pair)]
    [DataRow("Tree.Pair { Both: (_, _) }", Pair)]
    [DataRow("Tree.Pair { Both.Id: var id }", Pair)]
    [DataRow("Tree.Pair { Both: { Id: var id, Label: var label } }", Pair)]
    [DataRow("Tree.Spelled(var count)", Spelled)]
    [DataRow("Tree.Tagged((var tag, int weight))", Tagged)]
    [DataRow("Tree.Tagged { Mark: { Tag: var tag } }", Tagged)]
    [DataRow("Tree.Tagged { Mark.Weight: int weight }", Tagged)]
    public void SubpatternsThatMatchEveryValue_KeepTheArmOfTheCase(string pattern, string caseName)
    {
        var coverage = TreeCoverage($"{pattern} => 1, _ => 0");

        AssertOwnArm(coverage, caseName);
    }

    [TestMethod]
    [DataRow("Tree.Leaf(\"a\")", Leaf)]
    [DataRow("Tree.Leaf(not \"a\")", Leaf)]
    [DataRow("Tree.Leaf { Name.Length: > 0 }", Leaf)]
    [DataRow("Tree.Leaf { Name: [..] }", Leaf)]
    [DataRow("Tree.Leaf(not null and \"a\")", Leaf)]
    [DataRow("Tree.MaybeLeaf(string name)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf(object name)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf({ } name)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf(not null)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf(null)", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf { Name: string name }", MaybeLeaf)]
    [DataRow("Tree.MaybeLeaf { Name.Length: var length }", MaybeLeaf)]
    [DataRow("Tree.Node(Tree.Leaf, _)", Node)]
    [DataRow("Tree.Node(_, Tree.Node(_, _))", Node)]
    [DataRow("Tree.Node { Left: Tree.Leaf }", Node)]
    [DataRow("Tree.Counted(int count, _)", Counted)]
    [DataRow("Tree.Counted({ } count, _)", Counted)]
    [DataRow("Tree.Counted(not null, _)", Counted)]
    [DataRow("Tree.Counted(null, _)", Counted)]
    [DataRow("Tree.Counted(_, > 0)", Counted)]
    [DataRow("Tree.Counted(_, 0 or not 0)", Counted)]
    [DataRow("Tree.Pair((1, _))", Pair)]
    [DataRow("Tree.Pair((_, \"a\"))", Pair)]
    [DataRow("Tree.Pair { Both.Id: 1 }", Pair)]
    [DataRow("Tree.Spelled(int count)", Spelled)]
    [DataRow("Tree.Tagged((string tag, _))", Tagged)]
    [DataRow("Tree.Tagged { Mark: { Tag: string tag } }", Tagged)]
    [DataRow("Tree.Tagged { Mark.Tag: string tag }", Tagged)]
    public void SubpatternThatCanRejectAValue_TakesTheArmFromTheCase(string pattern, string caseName)
    {
        var coverage = TreeCoverage($"{pattern} => 1, _ => 0");

        AssertNoOwnArm(coverage, caseName);
        Assert.IsEmpty(coverage.UnhandledCases);
    }

    [TestMethod]
    public void TwoPartialArmsForOneCase_AreNotCombined()
    {
        var coverage = TreeCoverage("Tree.Counted(null, _) => 1, Tree.Counted(not null, _) => 2, _ => 0");

        AssertNoOwnArm(coverage, Counted);
    }

    [TestMethod]
    public void ExtensionDeconstruct_SubpatternsAreJudgedAgainstItsOutParameters()
    {
        var compilation = TestCompilation.Create("""
            namespace Notes
            {
                [Fprog.Union]
                public abstract class Note
                {
                    private Note() { }

                    public sealed class Plain : Note
                    {
                        public string Text { get; init; } = "";

                        public string? Author { get; init; }
                    }

                    public sealed class Titled : Note
                    {
                        public string Title { get; init; } = "";
                    }
                }

                public static class NoteExtensions
                {
                    public static void Deconstruct(this Note.Plain plain, out string text, out string? author)
                    {
                        text = plain.Text;
                        author = plain.Author;
                    }

                    extension(Note.Titled titled)
                    {
                        public void Deconstruct(out string title, out int length)
                        {
                            title = titled.Title;
                            length = titled.Title.Length;
                        }
                    }
                }

                public static class Switches
                {
                    public static int Typed(Note note) => note switch { Note.Plain(string text, var author) => 1, Note.Titled(string title, int length) => 2 };

                    public static int TypedAuthor(Note note) => note switch { Note.Plain(var text, string author) => 1, Note.Titled(_, > 0) => 2 };
                }
            }
            """);
        string[] cases = ["Notes.Note.Plain", "Notes.Note.Titled"];

        AssertCoverage(Coverage(compilation, "Typed"), cases, [], []);
        AssertCoverage(Coverage(compilation, "TypedAuthor"), cases, cases, cases);
    }

    [TestMethod]
    public void GenericExtensionDeconstruct_SubpatternsAreJudgedAgainstTheConstructedParameters()
    {
        var compilation = TestCompilation.Create("""
            namespace Boxes
            {
                [Fprog.Union]
                public abstract class Box<T>
                {
                    private Box() { }

                    public sealed class Full(T item, T? spare) : Box<T>
                    {
                        public T Item { get; } = item;

                        public T? Spare { get; } = spare;
                    }

                    public sealed class Empty : Box<T>;
                }

                public static class BoxExtensions
                {
                    public static void Deconstruct<T>(this Box<T>.Full full, out T item, out T? spare)
                    {
                        item = full.Item;
                        spare = full.Spare;
                    }
                }

                public static class Switches
                {
                    public static int Typed(Box<string> box) => box switch { Box<string>.Full(string item, _) => 1, Box<string>.Empty => 2 };

                    public static int TypedSpare(Box<string> box) => box switch { Box<string>.Full(_, string spare) => 1, Box<string>.Empty => 2 };
                }
            }
            """);
        string[] cases = ["Boxes.Box<T>.Full", "Boxes.Box<T>.Empty"];

        AssertCoverage(Coverage(compilation, "Typed"), cases, [], []);
        AssertCoverage(Coverage(compilation, "TypedSpare"), cases, ["Boxes.Box<T>.Full"], ["Boxes.Box<T>.Full"]);
    }

    [TestMethod]
    public void PositionalPatternThroughITuple_IsALengthTestAndHandlesNothing()
    {
        var compilation = TestCompilation.Create("""
            namespace Rows
            {
                [Fprog.Union]
                public abstract class Row : System.Runtime.CompilerServices.ITuple
                {
                    private Row() { }

                    public int Length => 2;

                    public object? this[int index] => null;

                    public sealed class Wide : Row { }
                }

                public static class Switches
                {
                    public static int Run(Row row) => row switch { (var first, var second) => 1 };
                }
            }
            """);

        AssertCoverage(Coverage(compilation, "Run"), ["Rows.Row.Wide"], ["Rows.Row.Wide"], ["Rows.Row.Wide"]);
    }

    [TestMethod]
    public void PositionalPatternThroughDeconstructOfTheUnion_IsACatchAll()
    {
        var compilation = TestCompilation.Create("""
            namespace Rows
            {
                [Fprog.Union]
                public abstract class Row
                {
                    private Row() { }

                    public void Deconstruct(out int width, out string? label)
                    {
                        width = 0;
                        label = null;
                    }

                    public sealed class Wide : Row { }

                    public sealed class Narrow : Row { }
                }

                public static class Switches
                {
                    public static int Vars(Row row) => row switch { Row.Wide => 1, var (width, label) => 2 };

                    public static int Typed(Row row) => row switch { Row.Wide => 1, (int width, string label) => 2 };
                }
            }
            """);
        string[] cases = ["Rows.Row.Wide", "Rows.Row.Narrow"];

        AssertCoverage(Coverage(compilation, "Vars"), cases, ["Rows.Row.Narrow"], []);
        AssertCoverage(Coverage(compilation, "Typed"), cases, ["Rows.Row.Narrow"], ["Rows.Row.Narrow"]);
    }

    [TestMethod]
    public void PositionalPatternThatDoesNotCompile_HandlesNothing()
    {
        var compilation = TestCompilation.CreateWithError(ShapeSource + """

            namespace Uses
            {
                using Shapes;

                public static class Switches
                {
                    public static int WithoutDeconstruct(Shape shape) => shape switch { var (first, second) => 1 };

                    public static int WrongCount(Shape shape) => shape switch { Shape.Circle => 1, Shape.Square(var first, var second) => 2 };
                }
            }
            """, "CS8129");

        AssertCoverage(Coverage(compilation, "WithoutDeconstruct"), [Circle, Square], [Circle, Square], [Circle, Square]);
        AssertCoverage(Coverage(compilation, "WrongCount"), [Circle, Square], [Square], [Square]);
    }

    [TestMethod]
    public void DefaultCoverage_HasNoCasesAndHandlesNothing()
    {
        var coverage = default(UnionSwitchCoverage);

        AssertCoverage(coverage, [], [], []);
        Assert.IsFalse(coverage.HandlesEveryCase);
    }

    private static string ShapeSwitch(string arms)
        => ShapeSource + $$"""

            namespace Uses
            {
                using Shapes;

                public static class Switches
                {
                    public static int Run(Shape shape) => shape switch { {{arms}} };
                }
            }
            """;

    /// <summary>The coverage of <c>shape switch { arms }</c> for a parameter <c>Shape shape</c>.</summary>
    private static UnionSwitchCoverage ShapeCoverage(string arms)
        => Coverage(TestCompilation.Create(ShapeSwitch(arms)), "Run");

    /// <summary>The coverage of <c>tree switch { arms }</c> for a parameter <c>Tree tree</c>.</summary>
    private static UnionSwitchCoverage TreeCoverage(string arms)
        => Coverage(
            TestCompilation.Create(TreeSource + $$"""

                namespace Uses
                {
                    using Trees;

                    public static class Switches
                    {
                        public static int Run(Tree tree) => tree switch { {{arms}} };
                    }
                }
                """),
            "Run");

    /// <summary>The coverage of <c>result switch { arms }</c> for a parameter <c>Result&lt;TValue&gt; result</c> of a generic method.</summary>
    private static UnionSwitchCoverage ResultCoverage(string arms)
        => Coverage(
            TestCompilation.Create(ResultSource + $$"""

                namespace Uses
                {
                    using Results;

                    public static class Switches
                    {
                        public static int Run<TValue>(Result<TValue> result) => result switch { {{arms}} };
                    }
                }
                """),
            "Run");

    private static UnionSwitchCoverage Coverage(Compilation compilation, string methodName)
    {
        Assert.IsTrue(new UnionModel(compilation).TryGetCoverage(compilation.GetSwitchExpression(methodName), out var coverage));
        return coverage;
    }

    private static NullableAnnotation ValueAnnotation(Compilation compilation, string methodName)
        => compilation.GetSwitchExpression(methodName).Value.Type?.NullableAnnotation ?? NullableAnnotation.None;

    private static void AssertOwnArm(UnionSwitchCoverage coverage, string caseName)
    {
        Assert.Contains(caseName, Names(coverage.Cases));
        Assert.DoesNotContain(caseName, Names(coverage.CasesWithoutOwnArm));
    }

    private static void AssertNoOwnArm(UnionSwitchCoverage coverage, string caseName)
        => Assert.Contains(caseName, Names(coverage.CasesWithoutOwnArm));

    private static void AssertCoverage(UnionSwitchCoverage coverage, string[] cases, string[] casesWithoutOwnArm, string[] unhandledCases)
    {
        AssertNames(cases, coverage.Cases);
        AssertNames(casesWithoutOwnArm, coverage.CasesWithoutOwnArm);
        AssertNames(unhandledCases, coverage.UnhandledCases);
    }

    private static void AssertNames(string[] expected, ImmutableArray<INamedTypeSymbol> actual)
        => Assert.AreSequenceEqual(expected, Names(actual));

    private static string[] Names(ImmutableArray<INamedTypeSymbol> types)
        => types.Select(type => type.ToDisplayString()).ToArray();
}
