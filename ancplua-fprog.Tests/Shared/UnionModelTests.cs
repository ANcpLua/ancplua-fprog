using System.Collections.Generic;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class UnionModelTests(TestContext testContext)
{
    private const string Hierarchy = """
                                     public closed record Shape;
                                     public sealed record Circle(int Radius) : Shape;
                                     public closed record Polygon : Shape;
                                     public sealed record Square(int Side) : Polygon;
                                     public sealed record Triangle(int Side) : Polygon;

                                     public closed class Result<T>;
                                     public sealed class Ok<T> : Result<T>;
                                     public sealed class Number : Result<int>;
                                     """;

    [TestMethod]
    public void NativeUnionGetsItsDeclaredCases()
    {
        var compilation = TestCompilation.Create("""
                                                 public sealed record Cat;
                                                 public sealed record Dog;
                                                 public union Pet(Cat, Dog);
                                                 """);
        var pet = compilation.GetNamedType("Pet");
        AssertCases(["Cat", "Dog"], UnionModel.GetCases(pet, testContext.CancellationToken));
    }

    [TestMethod]
    public void GenericUnionGetsItsSubstitutedCases()
    {
        var compilation = TestCompilation.Create("""
                                                 public union Choice<T>(T, string);
                                                 """);
        var choice = compilation.GetNamedType("Choice`1").Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        AssertCases(["int", "string"], UnionModel.GetCases(choice, testContext.CancellationToken));
    }

    [TestMethod]
    public void CustomUnionIgnoresConstructorsThatAreNotCaseConstructors()
    {
        var compilation = TestCompilation.Create("""
                                                 using System.Runtime.CompilerServices;

                                                 [Union]
                                                 public sealed class Choice : IUnion
                                                 {
                                                     public Choice(int value) => Value = value;
                                                     public Choice(string value) => Value = value;
                                                     public Choice(ref bool flag) : this(flag ? 1 : 0) { }
                                                     public object Value { get; }
                                                 }
                                                 """);
        AssertCases(["int", "string"], UnionModel.GetCases(compilation.GetNamedType("Choice"), testContext.CancellationToken));
    }

    [TestMethod]
    public void NullableUnionProvidesCasesThroughItsUnderlyingType()
    {
        var compilation = TestCompilation.Create("public union Choice(int, string);");
        var choice = compilation.GetNamedType("Choice");
        var nullable = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(choice);

        AssertCases([], nullable.UnionCaseTypes);
        var union = UnionModel.GetUnionType(nullable);
        Assert.IsNotNull(union);
        AssertCases(["int", "string"], UnionModel.GetCases(union, testContext.CancellationToken));
    }

    [TestMethod]
    public void ClosedHierarchyGetsDirectCasesAndExpandsClosedCaseToLeaves()
    {
        var compilation = TestCompilation.Create(Hierarchy);
        var shape = compilation.GetNamedType("Shape");
        AssertCases(["Circle", "Polygon"], UnionModel.GetCases(shape, testContext.CancellationToken));
        AssertCases(["Circle", "Square", "Triangle"], UnionModel.GetLeafCases(shape, testContext.CancellationToken));
        Assert.IsNull(UnionModel.GetUnionType(compilation.GetNamedType("Square")));
    }

    [TestMethod]
    public void ClosedHierarchyGetsCasesFromMetadata()
    {
        var library = TestCompilation.Create(Hierarchy, "Domain");
        var compilation = TestCompilation.Create("public sealed class Consumer;", "Consumer",
            TestCompilation.ToImageReference(library));
        AssertCases(["Circle", "Square", "Triangle"],
            UnionModel.GetLeafCases(compilation.GetNamedType("Shape"), testContext.CancellationToken));
    }

    [TestMethod]
    [DataRow(SpecialType.System_Int32, "Ok<int>", "Number")]
    [DataRow(SpecialType.System_String, "Ok<string>")]
    public void CompilerProvidesCasesForTheConstructedClosedType(SpecialType argument, params string[] expected)
    {
        var compilation = TestCompilation.Create(Hierarchy);
        var result = compilation.GetNamedType("Result`1").Construct(compilation.GetSpecialType(argument));
        AssertCases(expected, UnionModel.GetCases(result, testContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("(Shape shape)", "shape", "Shape")]
    [DataRow("(Shape? shape)", "shape", "Shape")]
    [DataRow("(Pet? pet)", "pet", "Pet")]
    [DataRow("(Shape shape)", "(object)shape", "Shape")]
    [DataRow("(Pet pet)", "(object)pet", "Pet")]
    [DataRow("(Circle circle)", "(object)(Shape)circle", "Shape")]
    [DataRow("(Polygon polygon)", "(Shape)polygon", "Polygon")]
    [DataRow("(object shape)", "(Shape)shape", "Shape")]
    [DataRow("(System.Collections.Generic.List<int> ints)", "(object)ints", null)]
    [DataRow("<T>(T shape) where T : Shape", "(object)shape", "Shape")]
    [DataRow("<T, U>(T shape) where T : U where U : Shape", "shape", "Shape")]
    [DataRow("<T>(T item) where T : class", "item", null)]
    public void UnionValueKeepsItsTypeThroughNullableConstraintsAndUpcasts(
        string signature, string value, string? expected)
    {
        var compilation = TestCompilation.Create(Hierarchy + $$"""

                                                            public union Pet(string, int);
                                                            public static class Sample
                                                            {
                                                                public static int Run{{signature}}
                                                                    => ({{value}}) switch { _ => 0 };
                                                            }
                                                            """);

        var operation = compilation.GetSwitchExpression("Run");
        Assert.AreEqual(expected, UnionModel.GetUnionOfExpression(operation.Value, compilation)?.Name);
    }

    [TestMethod]
    [DataRow("Circle", true)]
    [DataRow("Circle _", true)]
    [DataRow("Shape", true)]
    [DataRow("Circle(_)", true)]
    [DataRow("Circle(var radius)", true)]
    [DataRow("Circle { Radius: _ }", true)]
    [DataRow("Circle { Radius: > 0 }", false)]
    [DataRow("Circle(1)", false)]
    [DataRow("Circle or Square", true)]
    [DataRow("Square or Triangle", false)]
    [DataRow("not Square", false)]
    public void CoverageRequiresAnUnconditionalTestOfTheCase(string pattern, bool expected)
    {
        var compilation = TestCompilation.Create(Hierarchy + $$"""

                                                            public static class Sample
                                                            {
                                                                public static int Run(Shape shape)
                                                                    => shape switch { {{pattern}} => 1, _ => 0 };
                                                            }
                                                            """);
        var operation = compilation.GetSwitchExpression("Run");
        Assert.AreEqual(expected, UnionModel.CoversCase(
            compilation, operation.Arms[0].Pattern, compilation.GetNamedType("Circle")));
    }

    private static void AssertCases(string[] expected, IEnumerable<ITypeSymbol> cases)
    {
        CollectionAssert.AreEquivalent(expected, cases.Select(@case => @case.ToDisplayString()).ToArray());
    }
}
