using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0502Tests(TestContext testContext)
{
    private const string Shapes = """

                                  public interface IStraight;
                                  public closed record Shape;
                                  public sealed record Circle(int Radius) : Shape;
                                  public closed record Polygon : Shape;
                                  public sealed record Square(int Side) : Polygon, IStraight;
                                  public sealed record Triangle(int Side) : Polygon, IStraight;
                                  """;

    [TestMethod]
    public Task FP0502_SwitchStatementOverClosedConstraintMissingCase_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Size<T>(T shape) where T : Shape
                               {
                                   {|FP0502:switch|} (shape)
                                   {
                                       case Circle: return 1;
                                       case Square: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_CompleteSwitchStatementOverClosedConstraint_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Size<T>(T shape) where T : Shape
                               {
                                   switch (shape)
                                   {
                                       case Circle: return 1;
                                       case Square: return 2;
                                       case Triangle: return 3;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_SwitchStatementMissingCase_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Size(Shape shape)
                               {
                                   {|FP0502:switch|} (shape)
                                   {
                                       case Circle circle: return circle.Radius;
                                       case Square square: return square.Side;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    [DataRow("Circle when true")]
    [DataRow("Circle { Radius: > 0 }")]
    [DataRow("Circle(42)")]
    [DataRow("Circle when false")]
    public Task FP0502_CaseOnlyCoveredByRestriction_Reports(string label)
    {
        return VerifyAsync($$"""
                             public static class Area
                             {
                                 public static int Size(Shape shape)
                                 {
                                     {|FP0502:switch|} (shape)
                                     {
                                         case {{label}}: return 1;
                                         case Polygon: return 2;
                                     }

                                     return 0;
                                 }
                             }
                             """ + Shapes);
    }

    [TestMethod]
    [DataRow("Circle")]
    [DataRow("Circle circle")]
    [DataRow("Circle(_)")]
    [DataRow("Circle(var radius)")]
    [DataRow("Circle { Radius: _ }")]
    [DataRow("Circle { Radius: var radius }")]
    [DataRow("Circle { }")]
    public Task FP0502_UnconditionalPatternsHandleEntireCase_NoReport(string label)
    {
        return VerifyAsync($$"""
                             public static class Area
                             {
                                 public static int Size(Shape shape)
                                 {
                                     switch (shape)
                                     {
                                         case {{label}}: return 1;
                                         case Polygon: return 2;
                                     }

                                     return 0;
                                 }
                             }
                             """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_LeafTypesOrInterfaceOfTheBranchHandleItsCases_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int ByLeaves(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Circle or Square: return 1;
                                       case Triangle: return 2;
                                   }

                                   return 0;
                               }

                               public static int ByInterface(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Circle: return 1;
                                       case IStraight: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("case var unused")]
    [DataRow("case not Circle")]
    [DataRow("case { }")]
    [DataRow("case object")]
    public Task FP0502_CatchAllBelongsToFP0501_NoReport(string label)
    {
        return VerifyAsync($$"""
                             public static class Area
                             {
                                 public static int Size(Shape shape)
                                 {
                                     switch (shape)
                                     {
                                         case Circle: return 1;
                                         {{label}}: return 2;
                                     }

                                     return 0;
                                 }
                             }
                             """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_NativeUnionUnderBoxingAndNullableUnion_Reports()
    {
        return VerifyAsync("""
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Choice value)
                               {
                                   {|FP0502:switch|} ((object)value)
                                   {
                                       case int: return 1;
                                   }

                                   return 0;
                               }

                               public static int Maybe(Choice? value)
                               {
                                   {|FP0502:switch|} (value)
                                   {
                                       case string: return 1;
                                       case null: return 0;
                                   }

                                   return -1;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0502_UnionValueConstrainedToClosedClass_Reports()
    {
        return VerifyAsync("""
                           public static class Factory
                           {
                               public static int Tag<T>(T shape) where T : Shape
                               {
                                   {|FP0502:switch|} (shape)
                                   {
                                       case Circle: return 0;
                                   }

                                   return 1;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_NullIsHandledSeparatelyFromEveryCase_NoReport()
    {
        return VerifyAsync("""
                           #nullable enable
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Choice? value)
                               {
                                   switch (value)
                                   {
                                       case string: return 1;
                                       case int: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0502_SwitchExpressionAndNonUnionSwitch_NoReport()
    {
        return VerifyAsync("""
                           public static class Format
                           {
                               public static int CoveredByCompiler(Shape shape) => shape switch { Circle => 1 };

                               public static int Ordinary(object value)
                               {
                                   switch (value)
                                   {
                                       case string: return 1;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           public static class Area
                           {
                               public static int Size(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Circle: return 1;
                                   }

                                   return 0;
                               }
                           }
                           """ + Shapes);
    }

    [TestMethod]
    public Task FP0502_SwitchStatementsMissingReferencedCases_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0502", """
            using Domain;

            public static class Format
            {
                public static int Tag(Pet pet)
                {
                    {|FP0502:switch|} (pet)
                    {
                        case Cat: return 1;
                        case null: return 0;
                    }
                    return -1;
                }

                public static int Sides(Shape shape)
                {
                    {|FP0502:switch|} (shape)
                    {
                        case Circle: return 0;
                        case Square: return 4;
                    }
                    return -1;
                }
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0502_SwitchStatementsCoveringReferencedCases_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0502", """
            using Domain;

            public static class Format
            {
                public static int Tag(Pet pet)
                {
                    switch (pet)
                    {
                        case Cat: return 1;
                        case Dog: return 2;
                        case null: return 0;
                    }
                    return -1;
                }

                public static int Sides(Shape shape)
                {
                    switch (shape)
                    {
                        case Circle: return 0;
                        case Square: return 4;
                        case Triangle: return 3;
                    }
                    return -1;
                }
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0502_IncompleteGenericClosedHierarchy_NoReport(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            using Domain;

            public static class Format
            {
                public static int Tag<T>(Root<T> value, bool include)
                {
                    switch (value)
                    {
                        case Marked<T> when include: return 1;
                    }
                    return 0;
                }
            }
            """, fromReference);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0502_CompleteGenericClosedHierarchiesWithMissingCases_Reports(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            using System.Collections.Generic;
            using Domain;

            public static class Format
            {
                public static int Tag(Root<int> value, bool include)
                {
                    {|FP0502:switch|} (value)
                    {
                        case Marked<int> when include: return 1;
                    }
                    return 0;
                }

                public static int Tag(Root<List<int>> value)
                {
                    {|FP0502:switch|} (value)
                    {
                        case Marked<List<int>>: return 1;
                    }
                    return 0;
                }

                public static int Tag<T>(Result<T> value)
                {
                    {|FP0502:switch|} (value)
                    {
                        case Ok<T>: return 1;
                    }
                    return 0;
                }
            }
            """, fromReference);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0502_CompleteGenericClosedHierarchiesWithEveryCase_NoReport(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            using System.Collections.Generic;
            using Domain;

            public static class Format
            {
                public static int Tag(Root<int> value)
                {
                    switch (value)
                    {
                        case Marked<int>: return 1;
                    }
                    return 0;
                }

                public static int Tag(Root<List<int>> value)
                {
                    switch (value)
                    {
                        case Marked<List<int>>: return 1;
                        case Extra<int>: return 2;
                    }
                    return 0;
                }

                public static int Tag<T>(Result<T> value)
                {
                    switch (value)
                    {
                        case Ok<T>: return 1;
                        case Err<T>: return 2;
                    }
                    return 0;
                }
            }
            """, fromReference);
    }

    [TestMethod]
    public Task CompilerAcceptsCompleteSwitchExpressionOverClosedConstraint()
    {
        return FpAnalyzerTestHost.VerifyCompilerAsync("""
                                                      public static class Area
                                                      {
                                                          public static int Size<T>(T shape) where T : Shape => shape switch
                                                          {
                                                              Circle => 1,
                                                              Square => 2,
                                                              Triangle => 3
                                                          };
                                                      }
                                                      """ + Shapes, testContext.CancellationToken);
    }

    [TestMethod]
    public Task CompilerWarnsWhenSwitchExpressionMissesCase()
    {
        return FpAnalyzerTestHost.VerifyCompilerAsync("""
                                                      public static class Format
                                                      {
                                                          public static int Size(Shape shape) => shape {|#0:switch|} { Circle => 1 };
                                                      }
                                                      """ + Shapes, testContext.CancellationToken,
            DiagnosticResult.CompilerWarning("CS8509").WithLocation(0));
    }

    [TestMethod]
    public Task CompilerWarnsWhenSwitchExpressionMissesNull()
    {
        return FpAnalyzerTestHost.VerifyCompilerAsync("""
                                                      #nullable enable
                                                      public static class Format
                                                      {
                                                          public static int Size(Shape? shape) => shape {|#0:switch|} { Circle => 1, Polygon => 2 };
                                                      }
                                                      """ + Shapes, testContext.CancellationToken,
            DiagnosticResult.CompilerWarning("CS8655").WithLocation(0));
    }

    // Link is a closed case of Root and a hand-written union. As a case it stands for its subtype End;
    // what it holds in Value is not a Root.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public Task FP0502_ClosedCaseThatIsAlsoAUnion_Reports(bool fromReference)
    {
        const string library = """

            namespace Chains
            {
                using System.Runtime.CompilerServices;

                public closed class Root;

                [Union]
                public closed class Link : Root, IUnion
                {
                    public Link(Link next) => Value = next;
                    public Link(int value) => Value = value;
                    public object? Value { get; }
                }

                public sealed class End() : Link(0);
                public union Holder(Link, string);
            }
            """;
        const string source = """
            #nullable enable
            using Chains;

            public static class Format
            {
                public static int ByCase(Root value) { switch (value) { case Link: return 1; } return 0; }
                public static int ByLeaf(Root value) { switch (value) { case End: return 1; } return 0; }
                public static int Held(Holder value) { switch (value) { case Link: return 1; case string: return 2; } return 0; }
                public static int Missing(Root value) { {|FP0502:switch|} (value) { case null: return 1; } return 0; }
            }
            """;
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0502", source, "#nullable enable" + library,
                testContext.CancellationToken)
            : VerifyAsync(source + library);
    }

    [TestMethod]
    public Task FP0502_CatchAllOnAPropertyOfACase_Reports()
    {
        return VerifyAsync("""
                           public closed record Payment;
                           public sealed record Card : Payment;
                           public sealed record Cash : Payment;

                           public closed record Order;
                           public sealed record Paid(Payment Payment) : Order;
                           public sealed record Open : Order;

                           public static class Format
                           {
                               public static int Tag(Order order)
                               {
                                   {|FP0502:switch|} (order)
                                   {
                                       case Paid { Payment: var payment }: return payment is null ? 0 : 1;
                                   }

                                   return 2;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0502_AndPatternWhoseSidesCoverTheCase_NoReport()
    {
        return VerifyAsync("""
                           public interface ILabel;
                           public closed record Figure;
                           public sealed record Point : Figure, ILabel;
                           public sealed record Curve : Figure;

                           public static class Format
                           {
                               public static int Tag(Figure figure)
                               {
                                   switch (figure)
                                   {
                                       case Point and ILabel: return 1;
                                       case Curve: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0502_UnionNamingAClosedClassAndItsSubtype_Reports()
    {
        return VerifyAsync("""
                           public closed record Figure;
                           public sealed record Point : Figure;
                           public union Drawn(Figure, Point);

                           public static class Format
                           {
                               public static int Named(Drawn drawn)
                               {
                                   switch (drawn)
                                   {
                                       case Point: return 1;
                                   }

                                   return 0;
                               }

                               public static int Missing(Drawn drawn)
                               {
                                   {|FP0502:switch|} (drawn)
                                   {
                                       case null: return 1;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    // A value of V is an End<int>: the subtype of the constraint, not what a Link<int> holds in Value.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0502_TypeParameterConstrainedToClosedUnionClass_Reports(bool fromReference)
    {
        const string library = """

            namespace Chains
            {
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                public closed class Root<T>;

                [Union]
                public closed class Link<T> : Root<T>, IUnion
                {
                    public Link(int number) => Value = number;
                    public Link(string text) => Value = text;
                    public object? Value { get; }
                }

                public sealed class End<T>() : Link<T>(0);
                public sealed class Extra<T>() : Link<List<T>>(0);
            }
            """;
        const string source = """
            #nullable enable
            using Chains;

            public static class Format
            {
                public static int BySubtype<V>(V value) where V : Link<int>
                {
                    switch (value)
                    {
                        case End<int>: return 1;
                    }

                    return 0;
                }

                public static int ByContent<V>(V value) where V : Link<int>
                {
                    {|FP0502:switch|} (value)
                    {
                        case int: return 1;
                        case string: return 2;
                    }

                    return 0;
                }

                public static int Content(Link<int> value)
                {
                    switch (value)
                    {
                        case int: return 1;
                        case string: return 2;
                    }

                    return 0;
                }
            }
            """;
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0502", source, "#nullable enable" + library,
                testContext.CancellationToken)
            : VerifyAsync(source + library);
    }

    // Behind the cast the labels test the Choice wrapper, so neither label is ever taken.
    [TestMethod]
    public Task FP0502_CastInFrontOfUnionThatHoldsItsCase_Reports()
    {
        return VerifyAsync("""
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Choice choice)
                               {
                                   {|FP0502:switch|} ((object)choice)
                                   {
                                       case string: return 1;
                                       case int: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [TestMethod]
    public Task FP0502_CastInFrontOfClosedClassWithEveryCase_NoReport()
    {
        return VerifyAsync("""
                           public closed record Figure;
                           public sealed record Point : Figure;
                           public sealed record Curve : Figure;
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Figure figure)
                               {
                                   switch ((object)figure)
                                   {
                                       case Point: return 1;
                                       case Curve: return 2;
                                   }

                                   return 0;
                               }

                               public static int Tag(Choice choice)
                               {
                                   switch (choice)
                                   {
                                       case string: return 1;
                                       case int: return 2;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0502", source, testContext.CancellationToken);
    }

    private Task VerifyGenericClosedHierarchiesAsync(string source, bool fromReference)
    {
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync(
                "FP0502", source, ReferencedAssemblySources.GenericClosedHierarchies, testContext.CancellationToken)
            : VerifyAsync(source + "\n" + ReferencedAssemblySources.GenericClosedHierarchies);
    }
}
