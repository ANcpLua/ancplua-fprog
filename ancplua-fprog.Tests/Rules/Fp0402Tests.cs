using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0402Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0402_PublicConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public {|FP0402:Shape|}() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_ProtectedInternalAndPrivateProtectedConstructors_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            protected {|FP0402:Shape|}() { }

            internal {|FP0402:Shape|}(int sides) { }

            private protected {|FP0402:Shape|}(string name) { }

            protected internal {|FP0402:Shape|}(double size) { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_NoDeclaredConstructorReportsOnceAtUnionName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class {|FP0402:Shape|}
        {
            public sealed class Circle : Shape { }

            public sealed class Square : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_RecordWithoutDeclaredConstructorCopyConstructorIgnored_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record {|FP0402:Shape|}
        {
            public sealed record Circle(double Radius) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0402_RecordPrimaryConstructorReportedAtRecordName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record {|FP0402:Shape|}(int X)
        {
            public sealed record Circle(double Radius) : Shape(0);
        }
        """);

    [TestMethod]
    public Task FP0402_ClassPrimaryConstructorReportedAtClassName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class {|FP0402:Shape|}(int x)
        {
            public int X => x;

            public sealed class Circle() : Shape(0);
        }
        """);

    [TestMethod]
    public Task FP0402_PrimaryConstructorWithPrivateSecondaryConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record {|FP0402:Shape|}(int X)
        {
            private Shape() : this(0) { }

            public sealed record Circle(double Radius) : Shape(0);
        }
        """);

    [TestMethod]
    public Task FP0402_OnlyNonPrivateOfSeveralConstructors_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public {|FP0402:Shape|}(int sides) : this() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_ExplicitRecordCopyConstructorProtected_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            protected {|FP0402:Shape|}(Shape original) { }

            public sealed record Circle(double Radius) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0402_StaticConstructorOnlyCountsAsNoDeclaredConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class {|FP0402:Shape|}
        {
            static Shape() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_GenericUnionPublicConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            public {|FP0402:Result|}() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }
        """);

    [TestMethod]
    public Task FP0402_NonAbstractUnionWithoutConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0402:Shape|}
        {
            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_UnionNestedInOtherType_Reports() => VerifyAsync("""
        public static class Outer
        {
            [Fprog.Union]
            public abstract record {|FP0402:Shape|}
            {
                public sealed record Circle : Shape;
            }
        }
        """);

    [TestMethod]
    public Task FP0402_UnionInsideCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        [Fprog.Union]
        public abstract record {|FP0402:Shape|}
        {
            public sealed record Circle : Shape;
        }
        """);

    [TestMethod]
    public Task FP0402_PartialUnionWithoutConstructorReportsOnceAtFirstName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class {|FP0402:Shape|}
        {
            public sealed class Circle : Shape { }
        }

        public abstract partial class Shape
        {
            public sealed class Square : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_StaticUnionWithoutInstanceConstructorTreatedAsNoDeclaredConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public static class {|FP0402:Shape|}
        {
        }
        """);

    [TestMethod]
    public Task FP0402_PrivateConstructor_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_RecordWithPrivateConstructorAndGeneratedCopyConstructor_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            static Shape() { }

            public sealed record Circle(double Radius) : Shape;

            public sealed record Square(double Side) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0402_GenericUnionPrivateConstructor_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;
        }
        """);

    [TestMethod]
    public Task FP0402_CasePublicConstructors_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public sealed class Circle : Shape
            {
                public Circle(double radius) => Radius = radius;

                public double Radius { get; }
            }

            public sealed class Square : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_NonUnionWithPublicOrImplicitConstructor_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public abstract class Shape
        {
            public Shape() { }
        }

        public abstract record Animal(string Name);

        public class Plain { }
        """);

    [TestMethod]
    public Task FP0402_PartialConstructorReportedAtBothDeclarations_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class Shape
        {
            public partial {|FP0402:Shape|}(int sides);

            public sealed class Circle() : Shape(0);
        }

        public abstract partial class Shape
        {
            public partial {|FP0402:Shape|}(int sides) { }
        }
        """);

    [TestMethod]
    public Task FP0402_ConstructorInPartialDeclarationWithoutAttribute_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class Shape
        {
            public sealed class Circle : Shape { }
        }

        public abstract partial class Shape
        {
            protected {|FP0402:Shape|}() { }
        }
        """);

    [TestMethod]
    public Task FP0402_AttributeOnSecondPartialDeclarationReportsAtFirstName_Reports() => VerifyAsync("""
        public abstract partial class {|FP0402:Shape|}
        {
            public sealed class Circle : Shape { }
        }

        [Fprog.Union]
        public abstract partial class Shape;
        """);

    [TestMethod]
    public Task FP0402_PrimaryConstructorOnSecondPartialDeclaration_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial record Shape
        {
            public sealed record Circle(double Radius) : Shape(Radius);
        }

        public abstract partial record {|FP0402:Shape|}(double Size);
        """);

    [TestMethod]
    public Task FP0402_AliasAndGlobalQualifiedUnionAttribute_Reports() => VerifyAsync("""
        using U = Fprog.UnionAttribute;

        [U]
        public abstract class {|FP0402:Shape|}
        {
            public sealed class Circle : Shape { }
        }

        [global::Fprog.UnionAttribute]
        public abstract class Token
        {
            internal {|FP0402:Token|}() { }

            public sealed class Number : Token { }
        }
        """);

    [TestMethod]
    public Task FP0402_VerbatimIdentifierNamesIncludeAt_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class @Shape
        {
            public {|FP0402:@Shape|}() { }

            public sealed class Circle : Shape { }
        }

        [Fprog.Union]
        public abstract record {|FP0402:@Token|};
        """);

    [TestMethod]
    public Task FP0402_ConstructorWithAttributesCommentsAndLineBreaksSpanIsNameOnly_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            [System.Obsolete("old")]
            public /* not private */
                {|FP0402:Shape|}
                ( int sides ) { }

            private Shape() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0402_ExpressionBodiedExternAndUnsafeConstructors_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            protected {|FP0402:Shape|}(int sides) => Sides = sides;

            public extern {|FP0402:Shape|}(string name);

            internal unsafe {|FP0402:Shape|}(int* sides) => Sides = *sides;

            public int Sides { get; }

            public sealed class Circle() : Shape(0);
        }
        """);

    [TestMethod]
    public Task FP0402_EmptyAndGenericPrimaryConstructorsReportedAtName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record {|FP0402:Shape|}()
        {
            public sealed record Circle : Shape;
        }

        [Fprog.Union]
        public abstract record {|FP0402:Result|}<T>(T Value)
        {
            public sealed record Ok(T Value) : Result<T>(Value);
        }
        """);

    [TestMethod]
    public Task FP0402_PrimaryConstructorAndPublicSecondaryConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class {|FP0402:Shape|}(int sides)
        {
            public {|FP0402:Shape|}() : this(0) { }

            public int Sides => sides;

            public sealed class Circle() : Shape(1);
        }
        """);

    [TestMethod]
    public Task FP0402_UnionNestedInGenericTypeAndInsideUnion_Reports() => VerifyAsync("""
        public static class Outer<T>
        {
            [Fprog.Union]
            public abstract record Option
            {
                private protected {|FP0402:Option|}() { }

                public sealed record Some(T Value) : Option;
            }
        }

        [Fprog.Union]
        public abstract record Shape
        {
            private Shape() { }

            [Fprog.Union]
            public abstract record {|FP0402:Polygon|} : Shape
            {
                public sealed record Square(double Side) : Polygon;
            }
        }
        """);

    [TestMethod]
    public Task FP0402_RecordClassAndFileLocalUnions_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record class {|FP0402:Shape|}
        {
            public sealed record class Circle : Shape;
        }

        [Fprog.Union]
        file abstract class Token
        {
            public {|FP0402:Token|}() { }

            public sealed class Number : Token { }
        }
        """);

    [TestMethod]
    public Task FP0402_PrivateConstructorInPartialDeclarationWithoutAttribute_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class Shape
        {
            public sealed class Circle : Shape { }
        }

        public abstract partial class Shape
        {
            private Shape() { }
        }
        """);

    [TestMethod]
    public Task FP0402_PrivatePartialAndExpressionBodiedConstructors_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class Shape
        {
            private partial Shape(int sides);

            private Shape(string name) => Name = name;

            public string Name { get; } = "";

            public sealed class Circle() : Shape(1);
        }

        public abstract partial class Shape
        {
            private partial Shape(int sides) => Name = sides.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        """);

    [TestMethod]
    public Task FP0402_UnionDerivedFromNonUnionWithPublicConstructor_NoReport() => VerifyAsync("""
        public abstract class Entity
        {
            public Entity() { }

            protected Entity(int id) => Id = id;

            public int Id { get; }
        }

        [Fprog.Union]
        public abstract class Shape : Entity
        {
            private Shape() : base(1) { }

            public sealed class Circle : Shape { }
        }

        public abstract class Animal(string name)
        {
            public string Name => name;
        }
        """);

    [TestMethod]
    public Task FP0402_FunctionalCoreWithCasesAndAbstractRecordHierarchy_NoReport() => VerifyAsync("""
        using System.Collections.Immutable;
        using System.Linq;

        [Fprog.FunctionalCore]
        public static class Geometry
        {
            [Fprog.Union]
            public abstract record Shape
            {
                private Shape() { }

                public sealed record Circle(double Radius) : Shape;

                public sealed record Square(double Side) : Shape
                {
                    public Square() : this(1) { }
                }
            }

            public abstract record Measurement(double Value);

            public sealed record Area(double Value) : Measurement(Value);

            public static double Total(ImmutableArray<Shape> shapes)
                => shapes.Sum(static shape => shape switch
                {
                    Shape.Circle { Radius: var r } => r * r * 3.14,
                    Shape.Square s => s.Side * s.Side,
                    _ => 0
                });
        }
        """);

    [TestMethod]
    public Task FP0402_UnionAttributeFromOtherNamespace_NoReport() => VerifyAsync("""
        namespace Other.Fprog
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class UnionAttribute : System.Attribute { }

            [Union]
            public abstract class Shape
            {
                public Shape() { }
            }
        }
        """);

    [TestMethod]
    public Task FP0402_GeneratedCodeFile_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.Union]
        public abstract class Shape
        {
            public Shape() { }

            public sealed class Circle : Shape { }
        }

        [Fprog.Union]
        public abstract record Token;
        """);

    [TestMethod]
    public Task FP0402_MessageSaysWhichModifierToReplace_Reports() => VerifyAsync(
        """
        [Fprog.Union]
        public abstract class Shape
        {
            private protected {|#0:Shape|}() { }

            public sealed class Circle : Shape { }
        }
        """,
        Expected(0, "Union 'Shape' must have only private constructors: write 'private' instead of 'private protected' on this constructor, so that only its nested sealed cases can derive from it"));

    [TestMethod]
    public Task FP0402_MessageSaysWhichConstructorToDeclare_Reports() => VerifyAsync(
        """
        [Fprog.Union]
        public abstract record {|#0:Result|}<T>
        {
            public sealed record Ok(T Value) : Result<T>;
        }
        """,
        Expected(0, "Union 'Result' must have only private constructors: declare 'private Result() { }', so that only its nested sealed cases can derive from it"));

    [TestMethod]
    public Task FP0402_MessageSaysToReplacePrimaryConstructor_Reports() => VerifyAsync(
        """
        [Fprog.Union]
        public abstract record {|#0:Shape|}(int X)
        {
            public sealed record Circle(double Radius) : Shape(0);
        }
        """,
        Expected(0, "Union 'Shape' must have only private constructors: replace the primary constructor parameter list with a 'private Shape(...)' constructor that takes the same parameters, so that only its nested sealed cases can derive from it"));

    [TestMethod]
    public Task FP0402_IncompleteConstructorDoesNotCrash_Reports() => VerifyAsync(
        """
        [Fprog.Union]
        public abstract class Shape
        {
            public {|FP0402:Shape|}(int sides

            private Shape() { }

            public sealed class Circle : Shape { }
        }
        """,
        DiagnosticResult.CompilerError("CS0501").WithSpan(4, 12, 4, 17).WithArguments("Shape.Shape(int)"),
        DiagnosticResult.CompilerError("CS1002").WithSpan(4, 27, 4, 27),
        DiagnosticResult.CompilerError("CS1026").WithSpan(4, 27, 4, 27));

    private static DiagnosticResult Expected(int location, string message)
        => new DiagnosticResult("FP0402", DiagnosticSeverity.Warning).WithLocation(location).WithMessage(message);

    private Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0402", source, testContext.CancellationToken, expected);
}
