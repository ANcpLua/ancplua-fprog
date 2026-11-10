using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ancplua_fprog.Tests.Rules;

[TestClass]
public sealed class Fp0501Tests(TestContext testContext)
{
    private const string Shape = """

        public abstract record Entity;

        public interface IShape { }

        public interface IRound { }

        [Fprog.Union]
        public abstract record Shape : Entity, IShape
        {
            private Shape() { }

            public sealed record Circle(double Radius) : Shape, IRound;

            public sealed record Square(double Side) : Shape;
        }
        """;

    // Switch expressions: every catch-all form of the catalog row.

    [TestMethod]
    public Task FP0501_DiscardArm_Reports() => VerifyAsync("""
        public static class Area
        {
            public static double Of(Shape shape) => shape switch
            {
                Shape.Circle c => c.Radius,
                {|FP0501:_|} => 0
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_VarPatternArm_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:var other|} => other.GetHashCode()
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_VarDiscardArm_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:var _|} => 0
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_VarDeconstructionPatternOfUnion_Reports() => VerifyAsync("""
        public static class Area
        {
            public static double Of(Pair pair) => pair switch
            {
                Pair.Two => 2,
                {|FP0501:var (left, right)|} => left + right
            };
        }

        [Fprog.Union]
        public abstract record Pair
        {
            private Pair() { }

            public void Deconstruct(out double left, out double right) => (left, right) = (1, 2);

            public sealed record One : Pair;

            public sealed record Two : Pair;
        }
        """);

    [TestMethod]
    public Task FP0501_EmptyPropertyPatternArm_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_NotNullArm_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_TypePatternOfUnionItself_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:Shape|} => 0
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_DeclarationPatternOfUnionItself_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_ObjectTypeAndDeclarationPattern_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_BaseClassOfUnion_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:Entity entity|} => entity.GetHashCode()
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_InterfaceImplementedByUnionCountsAsBaseType_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:IShape|} => 0
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_UnionTypeWithEmptyPropertyPattern_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                {|FP0501:Shape { } other|} => other.GetHashCode()
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_CatchAllWithWhenClause_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_CatchAllOnOneSideOfTopLevelOr_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_CatchAllOnOneSideOfTopLevelAnd_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle and {|FP0501:var circle|} => circle.GetHashCode(),
                {|FP0501:not null|} and Shape.Square => 2
            };

            public static int Empty(Shape shape) => shape switch
            {
                Shape.Square and {|FP0501:{ }|} => 3,
                Shape.Circle => 1
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_ParenthesizedCatchAllReportsInnerPattern_Reports() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                ({|FP0501:_|}) => 0
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_InsideCoreNestedTypeAndAssemblyCore_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_InLambdaAndLocalFunction_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_NullableUnionValue_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_GenericUnionOfConstructedType_Reports() => VerifyAsync("""
        public static class Values
        {
            public static int Of(Result<int> result) => result switch
            {
                Result<int>.Ok ok => ok.Value,
                {|FP0501:Result<int> other|} => other.GetHashCode()
            };
        }

        [Fprog.Union]
        public abstract record Result<T>
        {
            private Result() { }

            public sealed record Ok(T Value) : Result<T>;

            public sealed record Error(string Message) : Result<T>;
        }
        """);

    [TestMethod]
    public Task FP0501_TypeParameterConstrainedToUnion_Reports() => VerifyAsync("""
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

    // Switch statements.

    [TestMethod]
    public Task FP0501_DefaultLabelReportsKeyword_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_DefaultLabelSharingSectionWithCase_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchStatementVarAndDiscard_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchStatementTypePatterns_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchStatementEmptyPropertyAndNotNull_Reports() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchStatementCatchAllWithWhen_Reports() => VerifyAsync("""
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

    // Similar code that is correct.

    [TestMethod]
    public Task FP0501_EveryCaseNamed_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_CatchAllInsideSubpattern_NoReport() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                Shape.Circle { Radius: _ } => 1,
                Shape.Square(var side) => (int)side,
                not Shape.Circle => 2
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_InterfaceImplementedOnlyByCase_NoReport() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape shape) => shape switch
            {
                IRound => 1,
                Shape.Square => 2
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_CatchAllInNestedSwitchOverComponent_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchOverNonUnionValue_NoReport() => VerifyAsync("""
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

    [TestMethod]
    public Task FP0501_SwitchOverCaseTypedValueIsNotUnionValue_NoReport() => VerifyAsync("""
        public static class Area
        {
            public static int Of(Shape.Circle circle) => circle switch
            {
                { Radius: > 1 } => 2,
                _ => 1
            };
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_IsExpressionIsNotASwitch_NoReport() => VerifyAsync("""
        public static class Area
        {
            public static bool Known(Shape shape) => shape is not null;

            public static bool Any(Shape shape) => shape is var other && other is { } && shape is Shape && shape is object;
        }
        """ + Shape);

    [TestMethod]
    public Task FP0501_UnionRecordWithSynthesizedMembersOnly_NoReport() => VerifyAsync(Shape);

    [TestMethod]
    public Task FP0501_GeneratedCode_NoReport() => VerifyAsync("""
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

    private Task VerifyAsync(string source)
        => FpAnalyzerTestHost.VerifyRuleAsync("FP0501", source, testContext.CancellationToken);
}
