using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0403Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0403_UnsealedNestedCase_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public class {|FP0403:Circle|} : Shape { }

            public sealed class Square : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0403_SealedCaseOutsideUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public sealed class Square : Shape { }
        }

        public sealed class {|FP0403:Circle|} : Shape { }
        """);

    [TestMethod]
    public Task FP0403_UnsealedAndNotNestedCaseOnlyOnce_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape { }

        public class {|FP0403:Circle|} : Shape { }
        """);

    [TestMethod]
    public Task FP0403_UnsealedRecordCase_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result
        {
            private Result() { }

            public record {|FP0403:Ok|}(int Value) : Result;

            public sealed record Error(string Message) : Result;
        }
        """);

    [TestMethod]
    public Task FP0403_SealedRecordCaseOutsideUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result;

        public sealed record {|FP0403:Ok|}(int Value) : Result;
        """);

    [TestMethod]
    public Task FP0403_AbstractCaseIsNotSealed_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public abstract class {|FP0403:Polygon|} : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0403_NestedUnionIsCaseOfOuterUnion_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0403_CaseNestedInAnotherType_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape { }

        public static class Shapes
        {
            public sealed class {|FP0403:Circle|} : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0403_CaseNestedDeeperInsideUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public static class Round
            {
                public sealed class {|FP0403:Circle|} : Shape { }
            }
        }
        """);

    [TestMethod]
    public Task FP0403_CaseNestedInSiblingCase_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public sealed class Square : Shape
            {
                public sealed class {|FP0403:Circle|} : Shape { }
            }
        }
        """);

    [TestMethod]
    public Task FP0403_CaseNestedInOtherUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape { }

        [Fprog.Union]
        public abstract class Color
        {
            public sealed class Red : Color { }

            public sealed class {|FP0403:Circle|} : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0403_PartialCaseReportedAtEveryDeclaration_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract partial class Shape
        {
            public partial class {|FP0403:Circle|} : Shape { }
        }

        public abstract partial class Shape
        {
            public partial class {|FP0403:Circle|}
            {
                public int Radius { get; init; }
            }
        }
        """);

    [TestMethod]
    public Task FP0403_PartialCaseSealedInOneDeclaration_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0403_GenericUnionCases_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            public sealed record Ok(T Value) : Result<T>;

            public record {|FP0403:Error|}(string Message) : Result<T>;

            public sealed record IntOk(int Value) : Result<int>;
        }

        public sealed record {|FP0403:TextOk|}(string Text) : Result<string>;
        """);

    [TestMethod]
    public Task FP0403_CaseInCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0403_CaseUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        [Fprog.Union]
        public abstract class Shape
        {
            public class {|FP0403:Circle|} : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0403_SealedNestedCases_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0403_UnsealedSubclassOfNonUnion_NoReport() => VerifyAsync("""
        public abstract class Animal
        {
            public class Dog : Animal { }
        }

        public class Cat : Animal { }

        public record Point(int X, int Y);

        public record Point3(int X, int Y, int Z) : Point(X, Y);
        """);

    [TestMethod]
    public Task FP0403_TypeDerivingFromCaseIsNoCase_Reports() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            public abstract class {|FP0403:Polygon|} : Shape { }
        }

        public class Triangle : Shape.Polygon { }
        """);

    [TestMethod]
    public Task FP0403_InterfaceImplementerAndUnionUser_NoReport() => VerifyAsync("""
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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0403", source, testContext.CancellationToken);
}
