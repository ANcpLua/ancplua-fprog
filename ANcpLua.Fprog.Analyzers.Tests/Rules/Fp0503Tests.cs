using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0503Tests(TestContext testContext)
{
    private const string Shapes = """

                                  public interface IRound;
                                  public closed record Shape;
                                  public sealed record Circle(int Radius) : Shape, IRound;
                                  public sealed record Square(int Side) : Shape;

                                  public union Choice(string, int);
                                  """;

    [TestMethod]
    public Task FP0503_IsWithTypeDeclarationOrRecursivePattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static bool IsCircle(Shape shape) => shape is {|FP0503:Circle|};
                               public static bool Positive(Shape shape) =>
                                   shape is {|FP0503:Circle circle|} && circle.Radius > 0;
                               public static bool PositiveRadius(Shape shape) =>
                                   shape is {|FP0503:Circle { Radius: > 0 }|};
                               public static bool Either(Shape shape) => shape is {|FP0503:Circle or Square|};
                               public static bool Negative(Shape shape) => shape is {|FP0503:not Square|};
                               public static bool Nested(Shape shape) => shape is {|FP0503:Circle { Radius: int radius }|} && radius > 0;
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0503_IsNativeUnionAndNullableUnionReportsOnlyPattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static bool IsText(Choice value) => value is {|FP0503:string|};
                               public static bool HasText(Choice? value) => value is {|FP0503:string text|} && text.Length > 0;
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0503_AsBehindCastOrThroughClosedConstraint_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           public static class Area
                           {
                               public static Circle? Round(Shape shape) => shape as {|FP0503:Circle|};
                               public static IRound? Boxed(Shape shape) => ((object)shape) as {|FP0503:IRound|};
                               public static Circle? Constrained<T>(T shape) where T : Shape
                                   => shape as {|FP0503:Circle|};
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0503_NullVarUntypedPatternAndSwitchCases_NoReport()
    {
        return VerifyAsync("""
                           #nullable enable
                           public static class Area
                           {
                               public static bool Missing(Shape? shape) => shape is null;
                               public static bool Present(Shape? shape) => shape is not null;
                               public static bool PresentPattern(Shape? shape) => shape is { };
                               public static bool Bind(Shape? shape) => shape is var current && current is not null;
                               public static int Switch(Shape shape) => shape switch { Circle => 1, Square => 2 };
                               public static bool Ordinary(object value) => value is Circle;
                               public static Circle? TryCircle(object value) => value as Circle;
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0503_TypeTestsOnReferencedUnions_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0503", """
            #nullable enable
            using Domain;

            public static class Checks
            {
                public static bool IsCat(Pet pet) => pet is {|FP0503:Cat|};
                public static Circle? AsCircle(Shape shape) => shape as {|FP0503:Circle|};
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0503_NullTestsOnReferencedUnions_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0503", """
            #nullable enable
            using Domain;

            public static class Checks
            {
                public static bool Empty(Pet pet) => pet is null;
                public static bool Missing(Shape? shape) => shape is null;
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0503_IncompleteGenericClosedHierarchy_NoReport(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            #nullable enable
            using Domain;

            public static class Checks
            {
                public static bool IsMarked<T>(Root<T> value) => value is Marked<T>;
                public static IMarked? AsMarked<T, TValue>(TValue value) where TValue : Root<T>
                    => (object)value as IMarked;
            }
            """, fromReference);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0503_CompleteGenericClosedHierarchies_Reports(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Domain;

            public static class Checks
            {
                public static bool IsMarked(Root<int> value) => value is {|FP0503:Marked<int>|};
                public static bool IsExtra(Root<List<int>> value) => value is {|FP0503:Extra<int>|};
                public static Ok<T>? AsOk<T, TValue>(TValue value) where TValue : Result<T>
                    => (object)value as {|FP0503:Ok<T>|};
            }
            """, fromReference);
    }

    [TestMethod]
    public Task FP0503_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           public static class ShapeChecks
                           {
                               public static bool IsRound(Shape shape) => shape is Circle;
                           }
                           """ + Shapes);
    }

    // Link<T> has a subtype that only exists at Link<List<T>>, so the compiler cannot list the subtypes of the
    // constraint. FP0601 covers the test in core.
    [TestMethod]
    public Task FP0503_TypeParameterConstrainedToIncompleteClosedUnionClass_NoReport()
    {
        return VerifyAsync("""
            #nullable enable
            using Chains;

            public static class Format
            {
                public static bool IsEnd<T, V>(V value) where V : Link<T> => value is End<T>;
            }

            namespace Chains
            {
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [Union]
                public closed class Link<T> : IUnion
                {
                    public Link(int number) => Value = number;
                    public Link(string text) => Value = text;
                    public object? Value { get; }
                }

                public sealed class End<T>() : Link<T>(0);
                public sealed class Extra<T>() : Link<List<T>>(0);
            }
            """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0503", source, testContext.CancellationToken);
    }

    private Task VerifyGenericClosedHierarchiesAsync(string source, bool fromReference)
    {
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync(
                "FP0503", source, ReferencedAssemblySources.GenericClosedHierarchies, testContext.CancellationToken)
            : VerifyAsync(source + "\n" + ReferencedAssemblySources.GenericClosedHierarchies);
    }
}
