using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0401Tests(TestContext testContext)
{
    [TestMethod]
    public Task FP0401_ConcreteClassUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:Shape|}
        {
            private Shape() { }

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_ConcreteRecordUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public record {|FP0401:Shape|}
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0401_PositionalRecordUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public record {|FP0401:Tagged|}(int Tag);
        """);

    [TestMethod]
    public Task FP0401_ExplicitRecordClassUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public record class {|FP0401:Tagged|}(int Tag);
        """);

    [TestMethod]
    public Task FP0401_SealedUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public sealed class {|FP0401:Single|}
        {
        }
        """);

    // Decided: Roslyn reports IsAbstract false for a static class (only its metadata is abstract),
    // so a static class with [Union] is not abstract and is reported.
    [TestMethod]
    public Task FP0401_StaticClassUnionIsNotAbstract_Reports() => VerifyAsync("""
        [Fprog.Union]
        public static class {|FP0401:Holder|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_GenericUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:Result|}<T>
        {
            private Result() { }

            public sealed class Ok(T value) : Result<T>
            {
                public T Value { get; } = value;
            }

            public sealed class Error(string message) : Result<T>
            {
                public string Message { get; } = message;
            }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionWithoutCases_Reports() => VerifyAsync("""
        [Fprog.Union]
        internal class {|FP0401:Empty|};
        """);

    [TestMethod]
    public Task FP0401_UnionNestedInOrdinaryType_Reports() => VerifyAsync("""
        public static class Outer<TOuter>
        {
            [Fprog.Union]
            private class {|FP0401:Inner|}
            {
            }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionInsideCore_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        [Fprog.Union]
        public class {|FP0401:Shape|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_UnionNestedInCoreType_Reports() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public static class Domain
        {
            [Fprog.Union]
            public class {|FP0401:Shape|}
            {
            }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionUnderAssemblyLevelCore_Reports() => VerifyAsync("""
        [assembly: Fprog.FunctionalCore]

        [Fprog.Union]
        public class {|FP0401:Shape|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_UnionInNamespace_Reports() => VerifyAsync("""
        namespace Shop.Orders
        {
            [Fprog.Union]
            public class {|FP0401:OrderState|}
            {
            }
        }
        """);

    // Decided: a partial union is reported at the name of every one of its declarations.
    [TestMethod]
    public Task FP0401_PartialUnionReportedAtEveryDeclaration_Reports() => VerifyAsync("""
        [Fprog.Union]
        public partial class {|FP0401:Shape|}
        {
        }

        public partial class {|FP0401:Shape|}
        {
            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_PartialUnionAbstractOnOneDeclaration_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public partial class Shape
        {
        }

        public abstract partial class Shape
        {
            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_AbstractClassUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public sealed class Circle : Shape { }

            public sealed class Square : Shape { }
        }
        """);

    // Synthesized record members (copy constructor, EqualityContract, Deconstruct, ...) are not reported.
    [TestMethod]
    public Task FP0401_AbstractRecordUnionWithSynthesizedMembers_NoReport() => VerifyAsync("""
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
        }
        """);

    [TestMethod]
    public Task FP0401_AbstractGenericUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }
        """);

    [TestMethod]
    public Task FP0401_ConcreteTypesWithoutUnionAttribute_NoReport() => VerifyAsync("""
        [Fprog.FunctionalCore]
        public class Plain
        {
        }

        public record Point(int X, int Y);

        public abstract class Animal
        {
        }

        public class Dog : Animal
        {
        }
        """);

    [TestMethod]
    public Task FP0401_ConcreteCaseOfAbstractUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract class Shape
        {
            private Shape() { }

            public class Circle : Shape { }
        }

        public class BigCircle : Shape.Circle
        {
        }
        """);

    [TestMethod]
    public Task FP0401_AttributeWithSameNameInOtherNamespace_NoReport() => VerifyAsync("""
        namespace Other
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class UnionAttribute : System.Attribute { }

            [Union]
            public class Shape
            {
            }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionAttributeIsNotInheritedByDerivedTypes_Reports() => VerifyAsync("""
        public class Base
        {
        }

        [Fprog.Union]
        public class {|FP0401:Derived|} : Base
        {
        }

        public class MoreDerived : Derived
        {
        }
        """);

    // Decided: a closed class (C# 15) is implicitly abstract; Roslyn 5.9.0 reports IsAbstract true,
    // so a closed union is abstract and is not reported.
    [TestMethod]
    public Task FP0401_ClosedClassUnionIsImplicitlyAbstract_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public closed class Shape
        {
            public sealed class Circle : Shape { }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0401", source, testContext.CancellationToken);
}
