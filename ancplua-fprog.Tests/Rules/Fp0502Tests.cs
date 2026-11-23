using System.Threading.Tasks;
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

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0502", source, testContext.CancellationToken);
    }
}
