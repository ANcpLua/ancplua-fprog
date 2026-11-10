using System.Threading.Tasks;
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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0402", source, testContext.CancellationToken);
}
