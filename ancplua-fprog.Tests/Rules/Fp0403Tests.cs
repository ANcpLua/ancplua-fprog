using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0403Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0403_UnsealedNestedCase_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               private Shape() { }

                               public class {|FP0403:Circle|} : Shape { }

                               public sealed class Square : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_SealedCaseOutsideUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public sealed class Square : Shape { }
                           }

                           public sealed class {|FP0403:Circle|} : Shape { }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnsealedAndNotNestedCaseOnlyOnce_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape { }

                           public class {|FP0403:Circle|} : Shape { }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnsealedRecordCase_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Result
                           {
                               private Result() { }

                               public record {|FP0403:Ok|}(int Value) : Result;

                               public sealed record Error(string Message) : Result;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_SealedRecordCaseOutsideUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Result;

                           public sealed record {|FP0403:Ok|}(int Value) : Result;
                           """);
    }

    [TestMethod]
    public Task FP0403_AbstractCaseIsNotSealed_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public abstract class {|FP0403:Polygon|} : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_NestedUnionIsCaseOfOuterUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Expr
                           {
                               [Fprog.Union]
                               public abstract record {|FP0403:Literal|} : Expr
                               {
                                   public sealed record Number(int Value) : Literal;
                               }

                               public sealed record Add(Expr Left, Expr Right) : Expr;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseNestedInAnotherType_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape { }

                           public static class Shapes
                           {
                               public sealed class {|FP0403:Circle|} : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseNestedDeeperInsideUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public static class Round
                               {
                                   public sealed class {|FP0403:Circle|} : Shape { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseNestedInSiblingCase_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public sealed class Square : Shape
                               {
                                   public sealed class {|FP0403:Circle|} : Shape { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseNestedInOtherUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape { }

                           [Fprog.Union]
                           public abstract class Color
                           {
                               public sealed class Red : Color { }

                               public sealed class {|FP0403:Circle|} : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseReportedAtFirstDeclaration_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract partial class Shape
                           {
                               public partial class {|FP0403:Circle|} : Shape { }
                           }

                           public abstract partial class Shape
                           {
                               public partial class Circle
                               {
                                   public int Radius { get; init; }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseSealedInOneDeclaration_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract partial class Shape
                           {
                               public sealed partial class Circle : Shape { }
                           }

                           public abstract partial class Shape
                           {
                               public partial class Circle
                               {
                                   public int Radius { get; init; }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_GenericUnionCases_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Result<T>
                           {
                               public sealed record Ok(T Value) : Result<T>;

                               public record {|FP0403:Error|}(string Message) : Result<T>;

                               public sealed record IntOk(int Value) : Result<int>;
                           }

                           public sealed record {|FP0403:TextOk|}(string Text) : Result<string>;
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseInCore_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Domain
                           {
                               [Fprog.Union]
                               public abstract record Shape
                               {
                                   public sealed record Square(double Side) : Shape;
                               }

                               public sealed record {|FP0403:Circle|}(double Radius) : Shape;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseUnderAssemblyLevelCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public class {|FP0403:Circle|} : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_SealedNestedCases_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Shape
                           {
                               private Shape() { }

                               public sealed record Circle(double Radius) : Shape;

                               public sealed record Square(double Side) : Shape;
                           }

                           [Fprog.Union]
                           public abstract class Option<T>
                           {
                               private Option() { }

                               public sealed class Some(T value) : Option<T>
                               {
                                   public T Value { get; } = value;
                               }

                               public sealed class None : Option<T> { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnsealedSubclassOfNonUnion_NoReport()
    {
        return VerifyAsync("""
                           public abstract class Animal
                           {
                               public class Dog : Animal { }
                           }

                           public class Cat : Animal { }

                           public record Point(int X, int Y);

                           public record Point3(int X, int Y, int Z) : Point(X, Y);
                           """);
    }

    [TestMethod]
    public Task FP0403_TypeDerivingFromCaseIsNoCase_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public abstract class {|FP0403:Polygon|} : Shape { }
                           }

                           public class Triangle : Shape.Polygon { }
                           """);
    }

    [TestMethod]
    public Task FP0403_InterfaceImplementerAndUnionUser_NoReport()
    {
        return VerifyAsync("""
                           public interface IShape { }

                           [Fprog.Union]
                           public abstract record Shape : IShape
                           {
                               public sealed record Circle(double Radius) : Shape;
                           }

                           public class Drawing : IShape
                           {
                               public Shape Main { get; init; } = new Shape.Circle(1);
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_GenericCaseSpanExcludesTypeParameters_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Option<T>
                           {
                               public class {|FP0403:Some|}<TValue> : Option<TValue> where TValue : notnull { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseWithAttributesCommentsAndLineBreaks_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               [System.Obsolete("old")]
                               public /* not sealed */ class
                                   /* name follows */ {|FP0403:Circle|} /* base follows */
                                   : Shape
                               {
                               }

                               /// <summary>A class with a primary constructor.</summary>
                               public class {|FP0403:Square|}(double side) : Shape
                               {
                                   public double Side { get; } = side;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_QualifiedAndAliasedBaseNames_Reports()
    {
        return VerifyAsync("""
                           using ShapeAlias = Geometry.Shapes.Shape;
                           using IntResult = Geometry.Results.Result<int>;

                           namespace Geometry.Shapes
                           {
                               [global::Fprog.UnionAttribute]
                               public abstract class Shape
                               {
                                   public sealed class Square : global::Geometry.Shapes.Shape { }
                               }
                           }

                           namespace Geometry.Results
                           {
                               [Fprog.Union()]
                               public abstract record Result<T>
                               {
                                   public sealed record Ok(T Value) : Result<T>;
                               }
                           }

                           namespace Drawing
                           {
                               public sealed class {|FP0403:Circle|} : ShapeAlias { }

                               public sealed record {|FP0403:Count|}(int Value) : IntResult;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_FileScopedNamespaceAndFileLocalCase_Reports()
    {
        return VerifyAsync("""
                           namespace Geometry;

                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public sealed class Square : Shape { }
                           }

                           file sealed class {|FP0403:Circle|} : Shape { }
                           """);
    }

    [TestMethod]
    public Task FP0403_NonAbstractUnionWithSealedNestedCases_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public class Shape
                           {
                               public sealed class Circle : Shape { }

                               public sealed record class Marker;
                           }

                           [Fprog.Union]
                           public record Result
                           {
                               public sealed record Ok(int Value) : Result;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseNestedInInterfaceStructOrRecordStruct_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape { }

                           public interface IShapes
                           {
                               sealed class {|FP0403:Circle|} : Shape { }
                           }

                           public readonly struct Holder
                           {
                               public sealed class {|FP0403:Square|} : Shape { }
                           }

                           public readonly record struct Pair(int A, int B)
                           {
                               public sealed class {|FP0403:Triangle|} : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseOfSameNamedUnionWithOtherArity_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public sealed class Square : Shape { }

                               public sealed class {|FP0403:Circle|} : Shape<int> { }
                           }

                           [Fprog.Union]
                           public abstract class Shape<T>
                           {
                               public sealed class Box(T content) : Shape<T>
                               {
                                   public T Content { get; } = content;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnionNestedInGenericType_Reports()
    {
        return VerifyAsync("""
                           public static class Outer<T>
                           {
                               [Fprog.Union]
                               public abstract record Shape
                               {
                                   public sealed record Circle(T Center) : Shape;

                                   public sealed record Square(int Side) : Outer<int>.Shape;
                               }

                               public sealed record {|FP0403:Triangle|}(T A) : Shape;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseOutsideUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape { }

                           public sealed partial class {|FP0403:Circle|} : Shape { }

                           public partial class Circle
                           {
                               public double Radius { get; init; }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseBaseListInLaterDeclaration_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract partial class Shape
                           {
                               public partial class {|FP0403:Circle|}
                               {
                                   public double Radius { get; init; }
                               }
                           }

                           public abstract partial class Shape
                           {
                               public partial class Circle : Shape { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseWithThreeDeclarations_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract partial record Result
                           {
                               public partial record {|FP0403:Ok|}(int Value) : Result;

                               public partial record Ok
                               {
                                   public bool IsZero => Value == 0;
                               }

                               public sealed record Error(string Message) : Result;
                           }

                           public abstract partial record Result
                           {
                               public partial record Ok
                               {
                                   public bool IsPositive => Value > 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseNestedDeeperInsideUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               public static partial class Round
                               {
                                   public sealed partial class {|FP0403:Circle|} : Shape { }
                               }

                               public static partial class Round
                               {
                                   public partial class Circle { }
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_PartialCaseSealedInLaterDeclaration_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract partial record Result
                           {
                               public partial record Ok(int Value) : Result;
                           }

                           public abstract partial record Result
                           {
                               public sealed partial record Ok
                               {
                                   public bool IsZero => Value == 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_CaseOfInnerUnionDeclaredInOuterUnion_Reports()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Expr
                           {
                               [Fprog.Union]
                               public abstract record {|FP0403:Literal|} : Expr
                               {
                                   public sealed record Text(string Value) : Literal;
                               }

                               public sealed record {|FP0403:Number|}(int Value) : Literal;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnionDerivingFromNonUnion_NoReport()
    {
        return VerifyAsync("""
                           public abstract class Entity
                           {
                               public int Id { get; init; }
                           }

                           [Fprog.Union]
                           public abstract class Shape : Entity
                           {
                               private Shape() { }

                               public sealed class Circle : Shape { }
                           }

                           public class Person : Entity { }
                           """);
    }

    [TestMethod]
    public Task FP0403_AnyAccessibilityAndUnsafeCase_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Shape
                           {
                               private sealed class Hidden : Shape { }

                               protected internal sealed class Shared(int value) : Shape
                               {
                                   public int Value { get; } = value;
                               }

                               internal sealed unsafe class Raw : Shape
                               {
                                   private readonly int* _pointer = null;

                                   public bool IsEmpty => _pointer == null;
                               }

                               public sealed class Empty : Shape;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_ExtensionBlockOnUnion_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Shape
                           {
                               public sealed record Circle(double Radius) : Shape;
                           }

                           public static class ShapeExtensions
                           {
                               extension(Shape shape)
                               {
                                   public bool IsRound => shape is Shape.Circle;
                               }

                               extension<T>(T value) where T : Shape
                               {
                                   public Shape AsShape() => value;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           [Fprog.Union]
                           public abstract class Shape { }

                           public class Circle : Shape { }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnionWithoutCases_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract class Empty
                           {
                               private Empty() { }

                               public static int Count => 0;
                           }

                           [Fprog.Union]
                           public abstract record Unit;
                           """);
    }

    [TestMethod]
    public Task FP0403_NonCaseTypesNestedInUnion_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Shape
                           {
                               private Shape() { }

                               public sealed record Circle(double Radius) : Shape;

                               public enum Kind { Round, Angular }

                               public interface IVisitor<out TResult>
                               {
                                   TResult Visit(Circle circle);
                               }

                               public readonly record struct Point(double X, double Y);

                               public delegate TResult Fold<out TResult>(Shape shape);

                               public static class Factory
                               {
                                   public static Shape Unit() => new Circle(1);
                               }

                               public record Tag(string Name);

                               public class Helper { }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0403_UnionNestedInsideCaseOfOtherUnion_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.Union]
                           public abstract record Document
                           {
                               private Document() { }

                               public sealed record Page(Page.Block Content) : Document
                               {
                                   [Fprog.Union]
                                   public abstract record Block
                                   {
                                       private Block() { }

                                       public sealed record Text(string Value) : Block;

                                       public sealed record Image(string Uri) : Block;
                                   }
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0403", source, testContext.CancellationToken);
    }
}