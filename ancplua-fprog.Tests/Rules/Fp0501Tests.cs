using System.Threading.Tasks;
using ancplua_fprog.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0501Tests(TestContext testContext)
{
    private const string Shape = """

                                 public abstract record Entity;

                                 public interface IShape { }

                                 public interface IRound { }

                                 public closed record Shape : Entity, IShape
                                 {
                                     public sealed record Circle(double Radius) : Shape, IRound;

                                     public sealed record Square(double Side) : Shape;
                                 }
                                 """;

    // Switch expressions: every catch-all form of the catalog row.

    [TestMethod]
    public Task FP0501_DiscardArm_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static double Of(Shape shape) => shape switch
                               {
                                   Shape.Circle c => c.Radius,
                                   {|FP0501:_|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_VarPatternArm_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:var other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_VarDiscardArm_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:var _|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_VarDeconstructionPatternOfUnion_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static double Of(Pair pair) => pair switch
                               {
                                   Pair.Two => 2,
                                   {|FP0501:var (left, right)|} => left + right
                               };
                           }

                           public closed record Pair
                           {
                               public void Deconstruct(out double left, out double right) => (left, right) = (1, 2);

                               public sealed record One : Pair;

                               public sealed record Two : Pair;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_EmptyPropertyPatternArm_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:{ }|} => 0
                               };

                               public static int Named(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:{ } other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_NotNullArm_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:not null|} => 0,
                                   null => -1
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_TypePatternOfUnionItself_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_DeclarationPatternOfUnionItself_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape other|} => other.GetHashCode()
                               };

                               public static int Discarded(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape _|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ObjectTypeAndDeclarationPattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:object|} => 0
                               };

                               public static int Named(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:object other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_BaseClassOfUnion_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Entity entity|} => entity.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_InterfaceImplementedByUnionCountsAsBaseType_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:IShape|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_UnionTypeWithEmptyPropertyPattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape { } other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllWithWhenClause_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape, bool strict) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} when strict => 0,
                                   Shape.Square => 2
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllOnOneSideOfTopLevelOr_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle or {|FP0501:_|} => 1
                               };

                               public static int Typed(Shape shape) => shape switch
                               {
                                   Shape.Square => 2,
                                   Shape.Circle or {|FP0501:Shape|} => 1
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllBeforeTheNarrowingSideOfAnd_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle and var circle => circle.GetHashCode(),
                                   {|FP0501:not null|} and Shape.Square => 2
                               };

                               public static int Empty(Shape shape) => shape switch
                               {
                                   Shape.Square and { } => 3,
                                   Shape.Circle => 1
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CaseAndVarIsNotACatchAll_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static double Of(Shape shape) => shape switch
                               {
                                   Shape.Circle { Radius: > 0 } and var circle => circle.Radius,
                                   Shape.Circle => 0,
                                   Shape.Square and { Side: var side } => side
                               };
                           }
                           """ + Shape);
    }

    // Both sides of the "and" are catch-alls (an "or" counts as one when either side is), so the arm
    // still matches every value.
    [TestMethod]
    public Task FP0501_AndOfTwoCatchAlls_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:not null|} and {|FP0501:var rest|} => rest.GetHashCode(),
                                   null => -1
                               };

                               public static int Nested(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape other|} and ({|FP0501:{ }|} and {|FP0501:not null|}) => other.GetHashCode()
                               };

                               public static int Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case (Shape.Square or {|FP0501:_|}) and {|FP0501:var rest|}:
                                           return rest.GetHashCode();
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllStillUsesTheInputTypeAtThatPosition_Reports()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   (Shape.Circle or Shape.Square) and {|FP0501:var known|} => known.GetHashCode()
                               };

                               public static double Named(Shape shape) => shape switch
                               {
                                   Shape.Circle and { } circle => circle.Radius,
                                   Shape.Square and not null => 0
                               };

                               public static int Rest(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:{ } rest|} and {|FP0501:not Shape.Circle|} => rest.GetHashCode()
                               };

                               public static int Grouped(Shape shape) => shape switch
                               {
                                   (Shape.Circle or {|FP0501:_|}) and Shape.Square => 2,
                                   Shape.Circle => 1
                               };

                               public static string Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case {|FP0501:not null|} and Shape.Square and var square:
                                           return square.ToString();
                                       case (Shape.Circle or Shape.Square) and {|FP0501:var known|}:
                                           return known.ToString();
                                       case null:
                                           return "none";
                                   }

                                   return "";
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ParenthesizedCatchAllReportsInnerPattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   ({|FP0501:_|}) => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_InsideCoreNestedTypeAndAssemblyCore_Reports()
    {
        return VerifyAsync("""
                           [assembly: Fprog.FunctionalCore]

                           [Fprog.FunctionalCore]
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static class Inner
                               {
                                   public static int Of(Shape shape) => shape switch
                                   {
                                       Shape.Circle => 1,
                                       {|FP0501:_|} => 0
                                   };
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_InLambdaAndLocalFunction_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static System.Func<Shape, int> Lambda() => shape => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static int Local(Shape shape)
                               {
                                   return Inner(shape);

                                   static int Inner(Shape value) => value switch
                                   {
                                       Shape.Circle => 1,
                                       {|FP0501:var other|} => 0
                                   };
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_NullableUnionValue_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           public static class Area
                           {
                               public static int Of(Shape? shape) => shape switch
                               {
                                   null => -1,
                                   Shape.Circle => 1,
                                   {|FP0501:Shape other|} => other.GetHashCode()
                               };

                               public static int Discard(Shape? shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_GenericUnionOfConstructedType_Reports()
    {
        return VerifyAsync("""
                           public static class Values
                           {
                               public static int Of(Result<int> result) => result switch
                               {
                                   Result<int>.Ok ok => ok.Value,
                                   {|FP0501:Result<int> other|} => other.GetHashCode()
                               };
                           }

                           public closed record Result<T>
                           {
                               public sealed record Ok(T Value) : Result<T>;

                               public sealed record Error(string Message) : Result<T>;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_TypeParameterConstrainedToUnion_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of<TShape>(TShape shape) where TShape : Shape => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static int Typed<TShape>(TShape shape) where TShape : Shape => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape other|} => other.GetHashCode()
                               };

                               public static int Self<TShape>(TShape shape) where TShape : Shape => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:TShape other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CompleteSwitchExpressionOverClosedConstraint_NoReport()
    {
        return VerifyAsync("""
                           public closed class Choice;
                           public sealed class Left : Choice;
                           public sealed class Right : Choice;

                           public static class Select
                           {
                               public static int Of<T>(T choice) where T : Choice => choice switch
                               {
                                   Left => 1,
                                   Right => 2
                               };
                           }
                           """);
    }

    // Switch statements.

    [TestMethod]
    public Task FP0501_DefaultLabelReportsKeyword_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       {|FP0501:default|}:
                                           return 0;
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_DefaultLabelSharingSectionWithCase_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                       {|FP0501:default|}:
                                           return 0;
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchStatementVarAndDiscard_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:var other|}:
                                           return other.GetHashCode();
                                   }

                                   return -1;
                               }

                               public static int Either(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle or {|FP0501:_|}:
                                           return 1;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchStatementTypePatterns_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Union(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:Shape|}:
                                           return 0;
                                   }

                                   return -1;
                               }

                               public static int Declared(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:Shape other|}:
                                           return other.GetHashCode();
                                   }

                                   return -1;
                               }

                               public static int Object(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:object|}:
                                           return 0;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchStatementEmptyPropertyAndNotNull_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Empty(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:{ }|}:
                                           return 0;
                                   }

                                   return -1;
                               }

                               public static int NotNull(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:not null|}:
                                           return 0;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchStatementCatchAllWithWhen_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape, bool strict)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case {|FP0501:var other|} when strict:
                                           return other.GetHashCode();
                                       {|FP0501:default|}:
                                           return 0;
                                   }
                               }
                           }
                           """ + Shape);
    }

    // Similar code that is correct.

    [TestMethod]
    public Task FP0501_EveryCaseNamed_NoReport()
    {
        return VerifyAsync("""
                           [Fprog.FunctionalCore]
                           public static class Area
                           {
                               public static double Of(Shape shape) => shape switch
                               {
                                   Shape.Circle c => c.Radius * c.Radius,
                                   Shape.Square { Side: var side } => side * side,
                                   null => 0
                               };

                               public static double Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle c:
                                           return c.Radius;
                                       case Shape.Square(var side) when side > 0:
                                           return side;
                                       case Shape.Square or null:
                                           return 0;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllInsideNonUnionSubpattern_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle { Radius: _ } => 1,
                                   Shape.Square(var side) => (int)side
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_InterfaceImplementedOnlyByCase_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   IRound => 1,
                                   Shape.Square => 2
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_CatchAllInNestedSwitchOverComponent_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle { Radius: var r } => r switch
                                   {
                                       > 1 => 2,
                                       _ => 1
                                   },
                                   Shape.Square => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchOverNonUnionValue_NoReport()
    {
        return VerifyAsync("""
                           public static class Days
                           {
                               public static string Name(int day) => day switch
                               {
                                   0 => "Sunday",
                                   _ => "Other"
                               };

                               public static int Of(object value)
                               {
                                   switch (value)
                                   {
                                       case string:
                                           return 1;
                                       default:
                                           return 0;
                                   }
                               }

                               public static int Abstract(Entity entity) => entity switch
                               {
                                   Shape.Circle => 1,
                                   _ => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchOverCaseTypedValueIsNotUnionValue_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape.Circle circle) => circle switch
                               {
                                   { Radius: > 1 } => 2,
                                   _ => 1
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_IsExpressionIsNotASwitch_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static bool Known(Shape shape) => shape is not null;

                               public static bool Any(Shape shape) => shape is var other && other is { } && shape is Shape && shape is object;
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_UnionRecordWithSynthesizedMembersOnly_NoReport()
    {
        return VerifyAsync(Shape);
    }

    [TestMethod]
    public Task FP0501_GeneratedCode_NoReport()
    {
        return VerifyAsync("""
                           // <auto-generated/>
                           public static class Area
                           {
                               public static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   _ => 0
                               };
                           }
                           """ + Shape);
    }

    // Second pass: other places a switch can live.

    [TestMethod]
    public Task FP0501_FieldAndPropertyInitializersWithPrimaryConstructor_Reports()
    {
        return VerifyAsync("""
                           public sealed class Holder(Shape shape)
                           {
                               private static readonly System.Func<Shape, int> Measure = value => value switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Shape|} => 0
                               };

                               private readonly int _tag = shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public int Tag { get; } = shape switch
                               {
                                   Shape.Square => 2,
                                   {|FP0501:var other|} => other.GetHashCode()
                               };

                               public int Sum => _tag + Tag + Measure(shape);
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ConstructorIndexerOperatorAndBlockAccessor_Reports()
    {
        return VerifyAsync("""
                           public readonly record struct Measure
                           {
                               public Measure(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           Value = 1;
                                           break;
                                       {|FP0501:default|}:
                                           Value = 0;
                                           break;
                                   }
                               }

                               public int Value { get; }

                               public int this[Shape shape] => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:{ }|} => 0
                               };

                               public static int operator +(Measure measure, Shape shape) => measure.Value + (shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:not null|} => 0,
                                   null => -1
                               });

                               public static int Block(Shape shape)
                               {
                                   return shape switch
                                   {
                                       Shape.Circle => 1,
                                       {|FP0501:Shape other|} => other.GetHashCode()
                                   };
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_PartialMethodInterfaceMemberAndGenericType_Reports()
    {
        return VerifyAsync("""
                           public static partial class Area
                           {
                               public static partial int Of(Shape shape);

                               public static partial int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };
                           }

                           public interface IMeasure
                           {
                               static int Of(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:object|} => 0
                               };
                           }

                           public sealed class Box<T>
                           {
                               public static int Of(Shape shape, T value) => shape switch
                               {
                                   Shape.Circle => value!.GetHashCode(),
                                   {|FP0501:_|} => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_AsyncIteratorAndQueryExpression_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;

                           public static class Area
                           {
                               public static async Task<int> OfAsync(Shape shape)
                               {
                                   await Task.Yield();
                                   return shape switch
                                   {
                                       Shape.Circle => 1,
                                       {|FP0501:_|} => 0
                                   };
                               }

                               public static IEnumerable<int> Each(IEnumerable<Shape> shapes)
                               {
                                   foreach (var shape in shapes)
                                   {
                                       yield return shape switch
                                       {
                                           Shape.Circle => 1,
                                           {|FP0501:var other|} => other.GetHashCode()
                                       };
                                   }
                               }

                               public static IEnumerable<int> Query(IEnumerable<Shape> shapes)
                                   => from shape in shapes
                                      let tag = shape switch { Shape.Circle => 1, {|FP0501:{ }|} => 0 }
                                      select tag;
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_InterpolationObjectInitializerAndCollectionExpression_Reports()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;

                           public sealed class Label
                           {
                               public string Text { get; init; } = "";

                               public static string Of(Shape shape) => $"{(shape switch { Shape.Circle => "c", {|FP0501:_|} => "?" })}";

                               public static Label Make(Shape shape) => new()
                               {
                                   Text = shape switch { Shape.Square => "s", {|FP0501:Shape other|} => other.ToString() }
                               };

                               public static ImmutableArray<int> Tags(Shape shape) => [shape switch { Shape.Circle => 1, {|FP0501:not null|} => 0, null => -1 }];
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ExtensionBlockMembers_Reports()
    {
        return VerifyAsync("""
                           public static class ShapeExtensions
                           {
                               extension(Shape shape)
                               {
                                   public int Tag => shape switch
                                   {
                                       Shape.Circle => 1,
                                       {|FP0501:_|} => 0
                                   };

                                   public int Rank()
                                   {
                                       switch (shape)
                                       {
                                           case Shape.Square:
                                               return 2;
                                           {|FP0501:default|}:
                                               return 0;
                                       }
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SwitchOverThisInsideUnion_Reports()
    {
        return VerifyAsync("""
                           public closed record Light
                           {
                               public int Tag => this switch
                               {
                                   Red => 1,
                                   {|FP0501:_|} => 0
                               };

                               public int Rank()
                               {
                                   switch (this)
                                   {
                                       case Green:
                                           return 2;
                                       case {|FP0501:Light|}:
                                           return 0;
                                   }

                                   return -1;
                               }

                               public sealed record Red : Light;

                               public sealed record Green : Light;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_ClassUnionNestedInGenericType_Reports()
    {
        return VerifyAsync("""
                           public static class Outer<T>
                           {
                               public closed class Option
                               {
                                   public sealed class Some(T value) : Option
                                   {
                                       public T Value { get; } = value;
                                   }

                                   public sealed class None : Option;
                               }

                               public static int Of(Option option) => option switch
                               {
                                   Option.Some => 1,
                                   {|FP0501:Outer<T>.Option other|} => other.GetHashCode()
                               };

                               public static int Closed(Outer<int>.Option option) => option switch
                               {
                                   Outer<int>.Option.Some some => some.Value,
                                   {|FP0501:_|} => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_NestedUnionValueAgainstOuterUnionType_Reports()
    {
        return VerifyAsync("""
                           public closed record Node
                           {
                               public sealed record Leaf : Node;

                               public closed record Branch : Node
                               {
                                   public sealed record Pair : Branch;

                                   public sealed record Triple : Branch;
                               }
                           }

                           public static class Walk
                           {
                               public static int Of(Node.Branch branch) => branch switch
                               {
                                   Node.Branch.Pair => 2,
                                   {|FP0501:Node other|} => other.GetHashCode()
                               };
                           }
                           """);
    }

    // Second pass: spans.

    [TestMethod]
    public Task FP0501_QualifiedAliasedAndCommentedPatterns_Reports()
    {
        return VerifyAsync("""
                           using S = Shape;
                           using O = System.Object;

                           public static class Area
                           {
                               public static int Global(Shape shape) => shape switch
                               {
                                   global::Shape.Circle => 1,
                                   {|FP0501:global::Shape other|} => other.GetHashCode()
                               };

                               public static int Alias(Shape shape) => shape switch
                               {
                                   S.Circle => 1,
                                   {|FP0501:S|} => 0
                               };

                               public static int AliasObject(Shape shape) => shape switch
                               {
                                   S.Circle => 1,
                                   {|FP0501:O other|} => other.GetHashCode()
                               };

                               public static int Qualified(Shape shape) => shape switch
                               {
                                   S.Circle => 1,
                                   {|FP0501:global::System.Object|} => 0
                               };

                               public static int Commented(Shape shape) => shape switch
                               {
                                   S.Circle => 1,
                                   {|FP0501:var /* the rest */
                                       other|} => other.GetHashCode()
                               };

                               public static int Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case S.Circle:
                                           return 1;
                                       case {|FP0501:global::Shape|}:
                                           return 0;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ParenthesizedPatternsReportInnerPattern_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Mixed(Shape shape) => shape switch
                               {
                                   (Shape.Circle or {|FP0501:_|}) and ({|FP0501:{ }|}) => 1
                               };

                               public static int Arm(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   (({|FP0501:Shape|})) => 0
                               };

                               public static int Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case ({|FP0501:Shape|}):
                                           return 0;
                                   }

                                   return -1;
                               }

                               public static int Object(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case (({|FP0501:object|})) when shape.GetHashCode() > 0:
                                           return 0;
                                   }

                                   return -1;
                               }

                               public static int Var(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle:
                                           return 1;
                                       case ({|FP0501:var other|}):
                                           return other.GetHashCode();
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_NotNullWrittenWithParenthesesOrTypedDefault_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Parenthesized(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:not (null)|} => 0,
                                   null => -1
                               };

                               public static int Typed(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:not default(Shape)|} => 0,
                                   null => -1
                               };
                           }
                           """ + Shape);
    }

    // Second pass: shapes of the switched value.

    [TestMethod]
    public Task FP0501_ConditionalAccessAndCastValue_Reports()
    {
        return VerifyAsync("""
                           #nullable enable
                           public sealed record Holder(Shape Shape);

                           public static class Area
                           {
                               public static int Conditional(Holder? holder) => holder?.Shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static int Cast(Entity entity) => ((Shape)entity) switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:Entity other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_ChainedTypeParameterAndImplementedInterfaces_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Chained<T, TBase>(T value) where T : TBase where TBase : Shape => value switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:TBase other|} => other.GetHashCode()
                               };

                               public static int Interface<T>(T value) where T : Shape => value switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:IShape|} => 0
                               };

                               public static int Equatable(Shape shape) => shape switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:System.IEquatable<Shape> other|} => other.GetHashCode()
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_DefaultLabelBesideOnlyNullCase_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Of(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case null:
                                           return -1;
                                       {|FP0501:default|}:
                                           return 0;
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_UnsafeExternAbstractAndEmptySwitch_Reports()
    {
        return VerifyAsync("""
                           public abstract unsafe class Native
                           {
                               [System.Runtime.InteropServices.DllImport("native")]
                               public static extern int Measure(int* value);

                               public abstract int Of(Shape shape);

                               public static int Read(Shape shape, int* value) => shape switch
                               {
                                   Shape.Circle => *value,
                                   {|FP0501:_|} => 0
                               };

                               public static int Empty(Shape shape)
                               {
                                   switch (shape)
                                   {
                                   }

                                   return 0;
                               }
                           }
                           """ + Shape);
    }

    // Second pass: correct code that looks similar.

    [TestMethod]
    public Task FP0501_CaseTypesWithEmptyPropertyPatternOrDiscardDesignation_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static double Of(Shape shape) => shape switch
                               {
                                   Shape.Circle { } c => c.Radius,
                                   Shape.Square _ => 0,
                                   null => -1
                               };

                               public static int Statement(Shape shape)
                               {
                                   switch (shape)
                                   {
                                       case Shape.Circle { }:
                                           return 1;
                                       case Shape.Square and { Side: > 0 }:
                                           return 2;
                                   }

                                   return -1;
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_PositionalPatternOverUnionWithVarOrDiscardComponents_NoReport()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static double Of(Pair pair) => pair switch
                               {
                                   Pair.One => 1,
                                   (var left, var right) => left + right
                               };

                               public static double Discards(Pair pair) => pair switch
                               {
                                   Pair.Two => 2,
                                   (_, _) => 0
                               };
                           }

                           public closed record Pair
                           {
                               public void Deconstruct(out double left, out double right) => (left, right) = (1, 2);

                               public sealed record One : Pair;

                               public sealed record Two : Pair;
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_SwitchOverBoxedValueOrTupleOfUnions_Reports()
    {
        return VerifyAsync("""
                           public static class Area
                           {
                               public static int Boxed(Shape shape) => ((object)shape) switch
                               {
                                   Shape.Circle => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static int Both(Shape left, Shape right) => (left, right) switch
                               {
                                   (Shape.Circle, {|FP0501:_|}) => 1,
                                   ({|FP0501:_|}, Shape.Circle) => 2,
                                   {|FP0501:_|} => 0
                               };

                               public static int Statement(Shape left, Shape right)
                               {
                                   switch (left, right)
                                   {
                                       case (Shape.Square, {|FP0501:var other|}):
                                           return other.GetHashCode();
                                       {|FP0501:default|}:
                                           return 0;
                                   }
                               }
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_FunctionalCoreWithLinqAndLocalMutation_NoReport()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           using System.Linq;

                           [Fprog.FunctionalCore]
                           public static class Geometry
                           {
                               public static double Total(ImmutableArray<Shape> shapes)
                               {
                                   var total = 0.0;
                                   foreach (var shape in shapes)
                                       total += Area(shape);

                                   return total;
                               }

                               public static double Area(Shape shape) => shape switch
                               {
                                   Shape.Circle { Radius: > 0 and var r } => r * r * 3.14,
                                   Shape.Circle => 0,
                                   Shape.Square(var side) => side * side,
                                   null => 0
                               };

                               public static ImmutableArray<string> Names(ImmutableArray<Shape> shapes)
                                   => [.. shapes.Select(shape => shape switch
                                   {
                                       Shape.Circle c when c.Radius is var r && r > 1 => "big circle",
                                       Shape.Circle => "circle",
                                       Shape.Square { Side: _ } => "square",
                                       null => "none"
                                   })];

                               public static int Nested(int day, Shape shape) => day switch
                               {
                                   0 => Area(shape) switch { > 1 => 1, _ => 0 },
                                   _ => shape switch { Shape.Circle => 2, Shape.Square => 3, null => 4 }
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_NativeUnionAndNullableNativeUnion_Reports()
    {
        return VerifyAsync("""
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Choice value) => value switch
                               {
                                   string => 1,
                                   {|FP0501:_|} => 0
                               };

                               public static int Maybe(Choice? value) => value switch
                               {
                                   null => -1,
                                   string => 1,
                                   {|FP0501:var other|} => 0
                               };

                               public static int Boxed(Choice value) => (object)value switch
                               {
                                   {|FP0501:object|} => 0
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_CatchAllInsidePropertiesAndPositionalPatterns_Reports()
    {
        return VerifyAsync("""
                           public sealed record Drawing(Shape Shape, (int Copies, Shape Print) PrintJob);

                           public static class Samples
                           {
                               public static int ByProperty(Drawing drawing) => drawing switch
                               {
                                   { Shape: {|FP0501:var selected|} } => selected.GetHashCode()
                               };

                               public static int ByPosition(Drawing drawing) => drawing switch
                               {
                                   Drawing(Shape.Circle, (_, Shape.Square)) => 1,
                                   Drawing({|FP0501:_|}, (_, {|FP0501:not Shape.Circle|})) => 2,
                                   _ => 0
                               };
                           }
                           """ + Shape);
    }

    [TestMethod]
    public Task FP0501_SharedInterfaceOfCases_Reports()
    {
        return VerifyAsync("""
                           public interface IStamped;
                           public closed record Proof;
                           public sealed record Ticket : Proof, IStamped;
                           public sealed record Receipt : Proof, IStamped;

                           public static class Samples
                           {
                               public static int Tag(Proof proof) => proof switch
                               {
                                   {|FP0501:IStamped stamped|} => 1
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_NativeCasesAndEmptyValue_NoReport()
    {
        return VerifyAsync("""
                           public union Choice(string, int);

                           public static class Format
                           {
                               public static int Tag(Choice value) => value switch
                               {
                                   string => 1,
                                   int => 2,
                                   null => -1
                               };
                           }
                           """);
    }

    [TestMethod]
    public Task FP0501_ExplicitOnlyCaseInClosedHierarchy_NoReport()
    {
        return VerifyAsync("""
                           public closed class Shape;
                           public sealed class Circle : Shape;

                           public static class Format
                           {
                               public static int Tag(Shape shape) => shape switch { Circle => 1 };
                           }
                           """);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0501_IncompleteGenericClosedHierarchy_NoReport(bool fromReference)
    {
        return VerifyGenericClosedHierarchiesAsync("""
            using Domain;

            public static class Format
            {
                public static int Tag<T>(Root<T> value) => value switch { Marked<T> => 1, _ => 0 };

                public static int TagByInterface<T>(Root<T> value)
                {
                    switch (value)
                    {
                        case IMarked: return 1;
                    }
                    return 0;
                }
            }
            """, fromReference);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FP0501_CompleteGenericClosedHierarchies_Reports(bool fromReference)
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
                        case {|FP0501:IMarked|}: return 1;
                    }
                    return 0;
                }

                public static int Tag(Root<List<int>> value) => value switch
                {
                    Marked<List<int>> => 1,
                    {|FP0501:_|} => 0
                };

                public static int Tag<T>(Result<T> value) => value switch { Ok<T> => 1, {|FP0501:_|} => 0 };
            }
            """, fromReference);
    }

    [TestMethod]
    public Task FP0501_CatchAllOverReferencedUnions_Reports()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0501", """
            using Domain;

            public static class Format
            {
                public static int Tag(Pet pet) => pet switch
                {
                    {|FP0501:IAnimal animal|} => 1,
                    null => 0
                };

                public static int Sides(Shape shape) => shape switch
                {
                    Circle => 0,
                    {|FP0501:_|} => 1
                };
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    [TestMethod]
    public Task FP0501_EveryReferencedCaseNamed_NoReport()
    {
        return FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync("FP0501", """
            using Domain;

            public static class Format
            {
                public static int Tag(Pet pet) => pet switch { Cat => 1, Dog => 2, null => 0 };
                public static int Sides(Shape shape) => shape switch { Circle => 0, Square => 4, Triangle => 3 };
            }
            """, ReferencedAssemblySources.Unions, testContext.CancellationToken);
    }

    private Task VerifyAsync(string source)
    {
        return FpAnalyzerTestHost.VerifyRuleAsync("FP0501", source, testContext.CancellationToken);
    }

    private Task VerifyGenericClosedHierarchiesAsync(string source, bool fromReference)
    {
        return fromReference
            ? FpAnalyzerTestHost.VerifyRuleWithReferencedAssemblyAsync(
                "FP0501", source, ReferencedAssemblySources.GenericClosedHierarchies, testContext.CancellationToken)
            : VerifyAsync(source + "\n" + ReferencedAssemblySources.GenericClosedHierarchies);
    }
}
