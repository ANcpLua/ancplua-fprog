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

    // The span is the identifier token, which includes the '@' of a verbatim identifier.
    [TestMethod]
    public Task FP0401_VerbatimIdentifierName_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:@Shape|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_NameAfterCommentsLineBreaksAndOtherAttributes_Reports() => VerifyAsync("""
        [System.Serializable, Fprog.Union]
        [System.Obsolete("old")]
        public /* not abstract */ sealed
            // the name follows on its own line
            record
            {|FP0401:Shape|}
            (
                int Tag
            );
        """);

    [TestMethod]
    public Task FP0401_AttributeThroughAliasGlobalAndTargetSpecifier_Reports() => VerifyAsync("""
        using UnionAlias = Fprog.UnionAttribute;

        [type: UnionAlias]
        public class {|FP0401:First|}
        {
        }

        [global::Fprog.UnionAttribute()]
        public class {|FP0401:Second|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_PartialUnionAttributeOnLastOfThreeDeclarations_Reports() => VerifyAsync("""
        public partial class {|FP0401:Shape|}
        {
        }

        public sealed partial class {|FP0401:Shape|}
        {
            public static int Count => 0;
        }

        [Fprog.Union]
        partial class {|FP0401:Shape|}
        {
        }
        """);

    [TestMethod]
    public Task FP0401_StaticPartialUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public static partial class {|FP0401:Holder|}
        {
        }

        public static partial class {|FP0401:Holder|}
        {
            public static int Zero() => 0;
        }
        """);

    // The union's name in nameof, typeof, cases and other attributes is not a report location.
    [TestMethod]
    public Task FP0401_UnionNameUsedInNameofTypeofAndCases_Reports() => VerifyAsync("""
        [Fprog.Union]
        [System.Obsolete(nameof(Shape))]
        public class {|FP0401:Shape|}
        {
            public const string Name = nameof(Shape);

            public static System.Type Type => typeof(Shape);

            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_ConcreteUnionNestedInConcreteUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:Outer|}
        {
            [Fprog.Union]
            public class {|FP0401:Inner|} : Outer
            {
                public sealed class Leaf : Inner { }
            }
        }
        """);

    [TestMethod]
    public Task FP0401_ConcreteUnionBaseOfAbstractUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:Base|}
        {
        }

        [Fprog.Union]
        public abstract class Derived : Base
        {
            public sealed class Leaf : Derived { }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionNestedInInterfaceStructAndRecordStruct_Reports() => VerifyAsync("""
        public interface IHost
        {
            [Fprog.Union]
            public class {|FP0401:InInterface|}
            {
            }
        }

        public readonly struct Holder
        {
            [Fprog.Union]
            internal class {|FP0401:InStruct|}
            {
            }
        }

        public readonly record struct Pair(int A, int B)
        {
            [Fprog.Union]
            private record {|FP0401:InRecordStruct|};
        }
        """);

    [TestMethod]
    public Task FP0401_FileLocalUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        file class {|FP0401:Hidden|}
        {
            public sealed class Case : Hidden { }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionWithPrimaryConstructor_Reports() => VerifyAsync("""
        [Fprog.Union]
        public class {|FP0401:Tagged|}(int tag)
        {
            public int Tag { get; } = tag;
        }
        """);

    [TestMethod]
    public Task FP0401_UnsafeUnion_Reports() => VerifyAsync("""
        [Fprog.Union]
        public unsafe class {|FP0401:Buffer|}
        {
            public static int Read(int* pointer) => *pointer;
        }
        """);

    [TestMethod]
    public Task FP0401_UnionNestedDeepInGenericTypes_Reports() => VerifyAsync("""
        public static class Outer<T>
            where T : class
        {
            public static class Middle<TMiddle>
                where TMiddle : notnull
            {
                [Fprog.Union]
                public class {|FP0401:Inner|}<TInner>
                    where TInner : struct, System.IComparable<TInner>
                {
                    public sealed class Case : Inner<TInner> { }
                }
            }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionHidingInheritedNestedType_Reports() => VerifyAsync("""
        public class Base
        {
            public class Shape
            {
            }
        }

        public class Derived : Base
        {
            [Fprog.Union]
            public new class {|FP0401:Shape|}
            {
            }
        }
        """);

    // An extension block (C# 14) inside a static union is its own type symbol; only the class is reported.
    [TestMethod]
    public Task FP0401_StaticUnionWithExtensionBlock_Reports() => VerifyAsync("""
        [Fprog.Union]
        public static class {|FP0401:Numbers|}
        {
            extension(int value)
            {
                public int Twice => value * 2;

                public static int Zero() => 0;
            }
        }
        """);

    [TestMethod]
    public Task FP0401_ClosedRecordUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public closed record Shape
        {
            public sealed record Circle(double Radius) : Shape;
        }
        """);

    [TestMethod]
    public Task FP0401_PartialUnionClosedOnOneDeclaration_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public partial class Shape
        {
        }

        public closed partial class Shape
        {
            public sealed class Circle : Shape { }
        }
        """);

    // Hidden lines count as generated code for the analyzer driver, so nothing is reported there.
    [TestMethod]
    public Task FP0401_UnionUnderLineHidden_NoReport() => VerifyAsync("""
        #line hidden
        [Fprog.Union]
        public class Shape
        {
        }
        #line default
        """);

    // Generated code is not analyzed (GeneratedCodeAnalysisFlags.None, AGENTS.md "Diagnostic conventions").
    [TestMethod]
    public Task FP0401_UnionInAutoGeneratedFile_NoReport() => VerifyAsync("""
        // <auto-generated/>
        [Fprog.Union]
        public class Shape
        {
            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_UnionWithGeneratedCodeAttribute_NoReport() => VerifyAsync("""
        [Fprog.Union]
        [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
        public class Shape
        {
            public sealed class Circle : Shape { }
        }
        """);

    [TestMethod]
    public Task FP0401_AbstractPositionalRecordUnion_NoReport() => VerifyAsync("""
        [Fprog.Union]
        public abstract record Shape(string Name)
        {
            public sealed record Circle(double Radius) : Shape("circle");

            public sealed record Square(double Side) : Shape("square");
        }
        """);

    [TestMethod]
    public Task FP0401_FunctionalCoreWithAbstractUnionAndConcreteRecords_NoReport() => VerifyAsync("""
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

                public sealed record Square(double Side) : Shape;
            }

            public sealed record Summary(int Count, double Total);

            public record Point(double X, double Y);

            public static double Area(Shape shape) => shape switch
            {
                Shape.Circle circle => 3.14 * circle.Radius * circle.Radius,
                Shape.Square square => square.Side * square.Side,
                _ => 0
            };

            public static Summary Summarize(ImmutableArray<Shape> shapes)
            {
                var total = 0.0;
                foreach (var shape in shapes)
                    total += Area(shape);

                return new Summary(shapes.Count(static shape => shape is Shape.Circle), total);
            }
        }
        """);

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0401", source, testContext.CancellationToken);
}
