using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Shared;

[TestClass]
public sealed class UnionModelTests
{
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

    private const string GenericCasesSource = """
        namespace Results
        {
            [Fprog.Union]
            public abstract record Result<T>
            {
                public sealed record Ok(T Value) : Result<T>;

                public sealed record Error(string Message) : Result<T>;
            }

            public sealed record IntOk(int Value) : Result<int>;

            public sealed record ListOk<T>(System.Collections.Generic.List<T> Values) : Result<System.Collections.Generic.List<T>>;

            public sealed record Both<TFirst, TSecond>(TFirst First, TSecond Second);

            public sealed record Same<T>(T Value) : Result<Both<T, T>>;

            public static class Values
            {
                public static void OfInt(Result<int> result) { }

                public static void OfString(Result<string> result) { }

                public static void OfListOfInt(Result<System.Collections.Generic.List<int>> result) { }

                public static void OfIntAndString(Result<Both<int, string>> result) { }

                public static void OfIntAndInt(Result<Both<int, int>> result) { }

                public static void OfAny<TValue>(Result<TValue> result) { }

                public static void OfAnyAndInt<TValue>(Result<Both<TValue, int>> result) { }

                public static void Constrained<TResult>(TResult result) where TResult : Result<int> { }
            }
        }
        """;

    private const string Ok = "Results.Result<T>.Ok";
    private const string Error = "Results.Result<T>.Error";
    private const string IntOk = "Results.IntOk";
    private const string ListOk = "Results.ListOk<T>";
    private const string Same = "Results.Same<T>";

    [TestMethod]
    public void UnionWithNestedSealedCases_HasThoseCases()
    {
        var compilation = TestCompilation.Create(ShapeSource);
        var shape = compilation.GetNamedType("Shapes.Shape");

        Assert.IsTrue(UnionModel.IsUnion(shape));
        AssertCases(["Shapes.Shape.Circle", "Shapes.Shape.Square"], new UnionModel(compilation).GetCases(shape));
        AssertSameSymbol(shape, UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.Shape+Circle")));
        AssertSameSymbol(shape, UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.Shape+Square")));
    }

    [TestMethod]
    public void CaseDeclaredOutsideItsUnion_IsACase()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public abstract class Shape { }

                public sealed class Triangle : Shape { }
            }
            """);
        var shape = compilation.GetNamedType("Shapes.Shape");

        AssertCases(["Shapes.Triangle"], new UnionModel(compilation).GetCases(shape));
        AssertSameSymbol(shape, UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.Triangle")));
    }

    [TestMethod]
    public void TypeDerivingFromACase_IsNotACase()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public abstract record Shape
                {
                    public record Circle(double Radius) : Shape;
                }

                public sealed record UnitCircle() : Shape.Circle(1.0);
            }
            """);

        Assert.IsNull(UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.UnitCircle")));
        AssertCases(["Shapes.Shape.Circle"], new UnionModel(compilation).GetCases(compilation.GetNamedType("Shapes.Shape")));
    }

    [TestMethod]
    public void GenericUnion_IsMatchedOnItsOriginalDefinition()
    {
        var compilation = TestCompilation.Create("""
            namespace Results
            {
                [Fprog.Union]
                public abstract record Result<T>
                {
                    public sealed record Ok(T Value) : Result<T>;

                    public sealed record Error(string Message) : Result<T>;
                }

                public sealed record Pending<T> : Result<T>;

                public static class Uses
                {
                    public static Result<int> Value { get; } = new Result<int>.Ok(1);

                    public static Result<string>.Ok Success { get; } = new("done");

                    public static Pending<int> Waiting { get; } = new();
                }
            }
            """);
        var result = compilation.GetNamedType("Results.Result`1");
        var uses = compilation.GetNamedType("Results.Uses");
        var resultOfInt = PropertyType(uses, "Value");

        Assert.IsTrue(UnionModel.IsUnion(resultOfInt));
        AssertSameSymbol(result, UnionModel.GetUnion(resultOfInt));
        AssertSameSymbol(result, UnionModel.GetUnionOfCase(PropertyType(uses, "Success")));
        AssertSameSymbol(result, UnionModel.GetUnionOfCase(PropertyType(uses, "Waiting")));
        AssertCases(
            ["Results.Result<T>.Ok", "Results.Result<T>.Error", "Results.Pending<T>"],
            new UnionModel(compilation).GetCases(resultOfInt));
    }

    [TestMethod]
    public void AbstractBaseClassWithoutTheAttribute_IsNotAUnion()
    {
        var compilation = TestCompilation.Create("""
            namespace Animals
            {
                public abstract class Animal { }

                public sealed class Dog : Animal { }
            }
            """);
        var animal = compilation.GetNamedType("Animals.Animal");

        Assert.IsFalse(UnionModel.IsUnion(animal));
        Assert.IsNull(UnionModel.GetUnion(animal));
        Assert.IsNull(UnionModel.GetUnionOfCase(compilation.GetNamedType("Animals.Dog")));
        Assert.IsEmpty(new UnionModel(compilation).GetCases(animal));
    }

    [TestMethod]
    public void ClassWithTheAttributeThatIsNotAbstract_IsAUnion()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                [Fprog.Union]
                public class Shape { }

                public class Circle : Shape { }
            }
            """);
        var shape = compilation.GetNamedType("Shapes.Shape");

        Assert.IsTrue(UnionModel.IsUnion(shape));
        AssertCases(["Shapes.Circle"], new UnionModel(compilation).GetCases(shape));
    }

    [TestMethod]
    public void AttributeWithTheSameSimpleNameButAnotherFullName_DoesNotMakeAUnion()
    {
        var compilation = TestCompilation.Create("""
            namespace Other
            {
                public sealed class UnionAttribute : System.Attribute { }
            }

            namespace Shapes
            {
                [Other.Union]
                public abstract class Shape { }

                public sealed class Circle : Shape { }
            }
            """);

        Assert.IsFalse(UnionModel.IsUnion(compilation.GetNamedType("Shapes.Shape")));
        Assert.IsNull(UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.Circle")));
    }

    [TestMethod]
    public void NullableAnnotatedUnionType_ResolvesToTheUnion()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                public static class Values
                {
                    public static Shapes.Shape? Maybe { get; } = null;
                }
            }
            """);
        var maybe = PropertyType(compilation.GetNamedType("Uses.Values"), "Maybe");

        Assert.AreEqual(NullableAnnotation.Annotated, maybe.NullableAnnotation);
        Assert.IsTrue(UnionModel.IsUnion(maybe));
        AssertSameSymbol(compilation.GetNamedType("Shapes.Shape"), UnionModel.GetUnion(maybe));
        AssertCases(["Shapes.Shape.Circle", "Shapes.Shape.Square"], new UnionModel(compilation).GetCases(maybe));
    }

    [TestMethod]
    public void NativeUnionDeclaration_IsNotAnFprogUnion()
    {
        var compilation = TestCompilation.Create("""
            namespace Pets
            {
                public sealed record Cat(string Name);

                public sealed record Dog(string Name);

                public union Pet(Cat, Dog);
            }
            """);
        var pet = compilation.GetNamedType("Pets.Pet");

        Assert.IsFalse(UnionModel.IsUnion(pet));
        Assert.IsNull(UnionModel.GetUnion(pet));
        Assert.IsEmpty(new UnionModel(compilation).GetCases(pet));
        Assert.IsNull(UnionModel.GetUnionOfCase(compilation.GetNamedType("Pets.Cat")));
    }

    [TestMethod]
    public void ClosedClass_IsNotAnFprogUnion()
    {
        var compilation = TestCompilation.Create("""
            namespace Shapes
            {
                public closed class Shape { }

                public sealed class Circle : Shape { }
            }
            """);
        var shape = compilation.GetNamedType("Shapes.Shape");

        Assert.IsFalse(UnionModel.IsUnion(shape));
        Assert.IsEmpty(new UnionModel(compilation).GetCases(shape));
        Assert.IsNull(UnionModel.GetUnionOfCase(compilation.GetNamedType("Shapes.Circle")));
    }

    [TestMethod]
    public void UnionFromAReferencedAssembly_HasCasesFromThatAssemblyAndThisCompilation()
    {
        var library = TestCompilation.Create("""
            namespace Lib
            {
                [Fprog.Union]
                public abstract class Shape { }

                public sealed class Circle : Shape { }
            }
            """, "Library");
        var compilation = TestCompilation.Create("""
            namespace App
            {
                public sealed class Square : Lib.Shape { }
            }
            """, "App", TestCompilation.ToImageReference(library));
        var shape = compilation.GetNamedType("Lib.Shape");

        Assert.IsTrue(UnionModel.IsUnion(shape));
        AssertSameSymbol(shape, UnionModel.GetUnionOfCase(compilation.GetNamedType("App.Square")));
        AssertCases(["Lib.Circle", "App.Square"], new UnionModel(compilation).GetCases(shape));
    }

    [TestMethod]
    public void GetCases_ForAConstructedUnion_LeavesOutCasesOfOtherConstructions()
    {
        var compilation = TestCompilation.Create(GenericCasesSource);
        var model = new UnionModel(compilation);

        AssertCases([Ok, Error, IntOk], model.GetCases(ParameterType(compilation, "OfInt")));
        AssertCases([Ok, Error], model.GetCases(ParameterType(compilation, "OfString")));
        AssertCases([Ok, Error, ListOk], model.GetCases(ParameterType(compilation, "OfListOfInt")));
    }

    [TestMethod]
    public void GetCases_ForTheUnionDefinition_ReturnsEveryCase()
    {
        var compilation = TestCompilation.Create(GenericCasesSource);

        AssertCases(
            [Ok, Error, IntOk, ListOk, Same],
            new UnionModel(compilation).GetCases(compilation.GetNamedType("Results.Result`1")));
    }

    [TestMethod]
    public void GetCases_CaseTypeParameterUsedTwice_MustBindToOneType()
    {
        var compilation = TestCompilation.Create(GenericCasesSource);
        var model = new UnionModel(compilation);

        AssertCases([Ok, Error], model.GetCases(ParameterType(compilation, "OfIntAndString")));
        AssertCases([Ok, Error, Same], model.GetCases(ParameterType(compilation, "OfIntAndInt")));
    }

    [TestMethod]
    public void GetCases_ForATypeParameterOfTheValue_ReturnsEveryCaseItCanBe()
    {
        var compilation = TestCompilation.Create(GenericCasesSource);
        var model = new UnionModel(compilation);

        AssertCases([Ok, Error, IntOk, ListOk, Same], model.GetCases(ParameterType(compilation, "OfAny")));
        AssertCases([Ok, Error, Same], model.GetCases(ParameterType(compilation, "OfAnyAndInt")));
    }

    [TestMethod]
    public void GetCases_ComparesTypeArgumentsByStructure()
    {
        var compilation = TestCompilation.Create("""
            namespace Structures
            {
                [Fprog.Union]
                public abstract record Result<T>;

                public sealed record ArrayOk : Result<int[]>;

                public sealed record TupleOk : Result<(int Id, string Name)>;

                public sealed record StringOk : Result<string>;

                public sealed record ObjectOk : Result<object>;

                public static class Outer<T>
                {
                    [Fprog.Union]
                    public abstract record Choice
                    {
                        public sealed record Yes(T Value) : Choice;
                    }
                }

                public sealed record IntChoice : Outer<int>.Choice;

                public static class Values
                {
                    public static void OfArray(Result<int[]> result) { }

                    public static void OfMatrix(Result<int[,]> result) { }

                    public static void OfStringArray(Result<string[]> result) { }

                    public static void OfUnnamedTuple(Result<(int, string)> result) { }

                    public static void OfOtherTuple(Result<(int, int)> result) { }

                    public static void OfNullableString(Result<string?> result) { }

                    public static void OfDynamic(Result<dynamic> result) { }

                    public static void OfIntChoice(Outer<int>.Choice choice) { }

                    public static void OfStringChoice(Outer<string>.Choice choice) { }
                }
            }
            """);
        var model = new UnionModel(compilation);

        AssertCases(["Structures.ArrayOk"], model.GetCases(ParameterType(compilation, "OfArray")));
        AssertCases([], model.GetCases(ParameterType(compilation, "OfMatrix")));
        AssertCases([], model.GetCases(ParameterType(compilation, "OfStringArray")));
        AssertCases(["Structures.TupleOk"], model.GetCases(ParameterType(compilation, "OfUnnamedTuple")));
        AssertCases([], model.GetCases(ParameterType(compilation, "OfOtherTuple")));
        AssertCases(["Structures.StringOk"], model.GetCases(ParameterType(compilation, "OfNullableString")));
        AssertCases(["Structures.ObjectOk"], model.GetCases(ParameterType(compilation, "OfDynamic")));
        AssertCases(
            ["Structures.Outer<T>.Choice.Yes", "Structures.IntChoice"],
            model.GetCases(ParameterType(compilation, "OfIntChoice")));
        AssertCases(["Structures.Outer<T>.Choice.Yes"], model.GetCases(ParameterType(compilation, "OfStringChoice")));
    }

    [TestMethod]
    public void TypeParameterConstrainedToAUnion_IsAUnionValue()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                public static class Constraints
                {
                    public static void Direct<TShape>(TShape shape) where TShape : Shapes.Shape { }

                    public static void Indirect<TOuter, TInner>(TOuter shape) where TOuter : TInner where TInner : Shapes.Shape { }

                    public static void NullableParameter<TShape>(TShape? shape) where TShape : Shapes.Shape { }

                    public static void NullableConstraint<TShape>(TShape shape) where TShape : Shapes.Shape? { }
                }
            }
            """);
        var shape = compilation.GetNamedType("Shapes.Shape");
        var model = new UnionModel(compilation);

        foreach (var method in new[] { "Direct", "Indirect", "NullableParameter", "NullableConstraint" })
        {
            var parameter = ParameterType(compilation, method);

            Assert.AreEqual(TypeKind.TypeParameter, parameter.TypeKind, method);
            Assert.IsFalse(UnionModel.IsUnion(parameter), method);
            AssertSameSymbol(shape, UnionModel.GetUnionType(parameter));
            AssertSameSymbol(shape, UnionModel.GetUnion(parameter));
            AssertCases(["Shapes.Shape.Circle", "Shapes.Shape.Square"], model.GetCases(parameter));
        }
    }

    [TestMethod]
    public void TypeParameterConstrainedToAConstructedUnion_HasThatUnionType()
    {
        var compilation = TestCompilation.Create(GenericCasesSource);
        var constrained = ParameterType(compilation, "Constrained");

        Assert.AreEqual("Results.Result<int>", UnionModel.GetUnionType(constrained)?.ToDisplayString());
        AssertSameSymbol(compilation.GetNamedType("Results.Result`1"), UnionModel.GetUnion(constrained));
        AssertCases([Ok, Error, IntOk], new UnionModel(compilation).GetCases(constrained));
    }

    [TestMethod]
    public void TypeParameterWithoutAUnionConstraint_IsNotAUnionValue()
    {
        var compilation = TestCompilation.Create(ShapeSource + """

            namespace Uses
            {
                public static class Constraints
                {
                    public static void Unconstrained<TValue>(TValue value) { }

                    public static void ReferenceType<TValue>(TValue value) where TValue : class { }

                    public static void Disposable<TValue>(TValue value) where TValue : System.IDisposable { }

                    public static void Other<TOuter, TInner>(TOuter value) where TOuter : TInner where TInner : System.Exception { }

                    public static void OfArray(Shapes.Shape[] shapes) { }

                    public static void OfCase(Shapes.Shape.Circle circle) { }
                }
            }
            """);
        var model = new UnionModel(compilation);

        foreach (var method in new[] { "Unconstrained", "ReferenceType", "Disposable", "Other", "OfArray", "OfCase" })
        {
            var parameter = ParameterType(compilation, method);

            Assert.IsNull(UnionModel.GetUnionType(parameter), method);
            Assert.IsNull(UnionModel.GetUnion(parameter), method);
            Assert.IsEmpty(model.GetCases(parameter), method);
        }

        Assert.IsNull(UnionModel.GetUnionType(null));
    }

    [TestMethod]
    public void CyclicConstraints_AreBrokenByRoslynAndGiveNoUnionValue()
    {
        var compilation = TestCompilation.CreateWithError("""
            namespace Uses
            {
                public static class Constraints
                {
                    public static void Cyclic<TFirst, TSecond>(TFirst value) where TFirst : TSecond where TSecond : TFirst { }
                }
            }
            """, "CS0454");
        var first = ParameterType(compilation, "Cyclic") as ITypeParameterSymbol;

        // Roslyn drops the constraint that closes the cycle, so the cycle guard of GetUnionType has
        // nothing to stop here. This pins that, and that the answer is null and not a hang.
        Assert.IsNotNull(first);
        var second = Assert.IsInstanceOfType<ITypeParameterSymbol>(Assert.ContainsSingle(first.ConstraintTypes));
        Assert.IsEmpty(second.ConstraintTypes);
        Assert.IsNull(UnionModel.GetUnionType(first));
        Assert.IsNull(UnionModel.GetUnionType(second));
    }

    [TestMethod]
    public void NullableAnnotatedConstructedUnionType_ResolvesToTheConstructedUnion()
    {
        var compilation = TestCompilation.Create(GenericCasesSource + """

            namespace Uses
            {
                public static class Maybe
                {
                    public static void OfMaybeInt(Results.Result<int>? result) { }
                }
            }
            """);
        var maybe = ParameterType(compilation, "OfMaybeInt");

        Assert.AreEqual(NullableAnnotation.Annotated, maybe.NullableAnnotation);
        AssertSameSymbol(maybe, UnionModel.GetUnionType(maybe));
        AssertSameSymbol(compilation.GetNamedType("Results.Result`1"), UnionModel.GetUnion(maybe));
        AssertCases([Ok, Error, IntOk], new UnionModel(compilation).GetCases(maybe));
    }

    [TestMethod]
    public void FirstUse_RacedByManyThreads_GivesEveryThreadTheSameAnswers()
    {
        const int unionCount = 8;
        const int threadCount = 16;
        var compilation = TestCompilation.Create(string.Concat(Enumerable.Range(0, unionCount).Select(ManyUnionsSource)));
        var expected = Enumerable.Range(0, unionCount).Select(ExpectedAnswers).ToArray();

        for (var round = 0; round < 20; round++)
        {
            // A new model has an empty index, so every round races to build it.
            var model = new UnionModel(compilation);
            var answers = Race(
                threadCount,
                thread =>
                {
                    // Every thread starts with another union, so the first calls differ.
                    var answersOfThread = new string[unionCount];
                    for (var step = 0; step < unionCount; step++)
                    {
                        var union = (thread + step) % unionCount;
                        answersOfThread[union] = Answers(model, compilation, union);
                    }

                    return answersOfThread;
                });

            foreach (var threadAnswers in answers)
                Assert.AreSequenceEqual(expected, threadAnswers);
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> on <paramref name="threadCount"/> threads of their own and lets all
    /// of them begin at the same moment.
    /// </summary>
    private static TResult[] Race<TResult>(int threadCount, Func<int, TResult> work)
    {
        var waiting = 0;
        var start = new TaskCompletionSource();
        var threads = Enumerable.Range(0, threadCount)
            .Select(thread => Task.Factory.StartNew(
                () =>
                {
                    Interlocked.Increment(ref waiting);
                    start.Task.Wait();
                    return work(thread);
                },
                TaskCreationOptions.LongRunning))
            .ToArray();

        SpinWait.SpinUntil(() => Volatile.Read(ref waiting) == threadCount);
        start.SetResult();
        return Task.WhenAll(threads).GetAwaiter().GetResult();
    }

    private static string ManyUnionsSource(int index)
        => $$"""
            namespace Many
            {
                [Fprog.Union]
                public abstract record Union{{index}}<T>
                {
                    public sealed record First(T Value) : Union{{index}}<T>;

                    public sealed record Second : Union{{index}}<T>;
                }

                public sealed record Third{{index}} : Union{{index}}<int>;

                public static class Switches{{index}}
                {
                    public static int Run{{index}}(Union{{index}}<string> value) => value switch { Union{{index}}<string>.First => 1, _ => 2 };
                }
            }

            """;

    /// <summary>What <see cref="Answers"/> must return for the union with the number <paramref name="index"/>.</summary>
    private static string ExpectedAnswers(int index)
    {
        var first = $"Many.Union{index}<T>.First";
        var second = $"Many.Union{index}<T>.Second";
        return $"definition: {first}, {second}, Many.Third{index}; value: {first}, {second}; not named: {second}; without own arm: {second}; unhandled: ";
    }

    private static string Answers(UnionModel model, Compilation compilation, int index)
    {
        var switchExpression = compilation.GetSwitchExpression($"Run{index}");
        Assert.IsTrue(model.TryGetCoverage(switchExpression, out var coverage));
        return $"definition: {Names(model.GetCases(compilation.GetNamedType($"Many.Union{index}`1")))}"
               + $"; value: {Names(coverage.Cases)}"
               + $"; not named: {Names(coverage.CasesNotNamed)}"
               + $"; without own arm: {Names(coverage.CasesWithoutOwnArm)}"
               + $"; unhandled: {Names(coverage.UnhandledCases)}";
    }

    private static string Names(ImmutableArray<INamedTypeSymbol> types)
        => string.Join(", ", types.Select(type => type.ToDisplayString()));

    private static ITypeSymbol ParameterType(Compilation compilation, string methodName)
    {
        var declaration = compilation.GetMethodDeclaration(methodName);
        var parameter = compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration.ParameterList.Parameters.Single());
        Assert.IsNotNull(parameter, $"No parameter symbol for '{methodName}'.");
        return parameter.Type;
    }

    private static ITypeSymbol PropertyType(INamedTypeSymbol type, string propertyName)
        => type.GetMembers(propertyName).OfType<IPropertySymbol>().Single().Type;

    private static void AssertCases(string[] expected, ImmutableArray<INamedTypeSymbol> actual)
        => Assert.AreSequenceEqual(expected, actual.Select(type => type.ToDisplayString()).ToArray());

    private static void AssertSameSymbol(ISymbol expected, ISymbol? actual)
        => Assert.AreEqual(expected, actual, SymbolEqualityComparer.Default);
}
