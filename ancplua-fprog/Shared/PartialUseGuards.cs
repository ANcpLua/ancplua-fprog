using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>Recognizes preceding conditions on the subject of a partial operation.</summary>
internal static class PartialUseGuards
{
    public static bool GuardsPosition(IOperation use, IOperation? collection, ImmutableArray<IOperation> indices)
    {
        return AnyTest(use, operation => operation switch
        {
            IPropertyReferenceOperation property => IsSizeProperty(property.Property.Name)
                                                    && SameReference(property.Instance, collection),
            IInvocationOperation invocation => IsAnyOn(invocation, collection),
            IIsPatternOperation pattern => SameReference(pattern.Value, collection) && TestsCollectionSize(pattern.Pattern),
            IBinaryOperation binary when IsComparison(binary.OperatorKind)
                => indices.Any(index => ComparesIndexVariable(binary, index)),
            _ => false
        });
    }

    public static bool GuardsDivisor(IOperation use, IOperation divisor)
    {
        return AnyTest(use, operation => operation switch
        {
            IBinaryOperation binary when IsComparison(binary.OperatorKind)
                => SameReference(binary.LeftOperand, divisor) || SameReference(binary.RightOperand, divisor),
            IIsPatternOperation pattern => SameReference(pattern.Value, divisor),
            IIsTypeOperation typeTest => SameReference(typeTest.ValueOperand, divisor),
            _ => false
        });
    }

    public static bool GuardsNullableValue(IPropertyReferenceOperation use)
    {
        return AnyTest(use, operation => operation switch
        {
            IPropertyReferenceOperation { Property.Name: "HasValue" } property
                => SameReference(property.Instance, use.Instance),
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } equality
                => SameReference(equality.LeftOperand, use.Instance) && IsNull(equality.RightOperand)
                   || SameReference(equality.RightOperand, use.Instance) && IsNull(equality.LeftOperand),
            IIsPatternOperation pattern => SameReference(pattern.Value, use.Instance) && TestsForNull(pattern.Pattern),
            IIsTypeOperation typeTest => SameReference(typeTest.ValueOperand, use.Instance),
            _ => false
        });
    }

    private static bool AnyTest(IOperation use, Func<IOperation, bool> predicate)
    {
        bool Tests(IOperation condition) => condition.DescendantsAndSelf().Any(predicate);

        for (var child = use; child.Parent is { } parent; child = parent)
        {
            if (EnclosingCondition(parent, child) is { } condition && Tests(condition))
                return true;

            if (parent is ISwitchCaseOperation @case && @case.Body.Contains(child)
                && !@case.Clauses.IsEmpty && @case.Clauses.All(clause =>
                    clause is IPatternCaseClauseOperation { Guard: { } guard } && Tests(guard)))
                return true;

            if (parent is not IBlockOperation block)
                continue;

            foreach (var statement in block.Operations)
            {
                if (statement == child)
                    break;

                if (statement is IConditionalOperation { Syntax: IfStatementSyntax } preceding && Tests(preceding.Condition))
                    return true;
            }
        }

        return false;
    }

    private static IOperation? EnclosingCondition(IOperation parent, IOperation child)
    {
        return parent switch
        {
            IConditionalOperation conditional when child != conditional.Condition => conditional.Condition,
            IWhileLoopOperation { ConditionIsTop: true } loop when child == loop.Body => loop.Condition,
            IForLoopOperation loop when child == loop.Body || loop.AtLoopBottom.Contains(child) => loop.Condition,
            IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr }
                binary when child == binary.RightOperand => binary.LeftOperand,
            ISwitchExpressionArmOperation arm when child == arm.Value => arm.Guard,
            ICatchClauseOperation @catch when child == @catch.Handler => @catch.Filter,
            _ => null
        };
    }

    private static bool ComparesIndexVariable(IBinaryOperation comparison, IOperation? index)
    {
        return Unwrap(index) switch
        {
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat } fromEnd => ComparesIndexVariable(comparison, fromEnd.Operand),
            IConversionOperation
            {
                OperatorMethod.ContainingType:
                {
                    MetadataName: "Index",
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                },
                Operand.Type.SpecialType: SpecialType.System_Int32
            } conversion => ComparesIndexVariable(comparison, conversion.Operand),
            IRangeOperation range => ComparesIndexVariable(comparison, range.LeftOperand)
                                     || ComparesIndexVariable(comparison, range.RightOperand),
            { } variable => SameReference(comparison.LeftOperand, variable) || SameReference(comparison.RightOperand, variable),
            null => false
        };
    }

    private static bool IsComparison(BinaryOperatorKind kind)
    {
        return kind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
            or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual;
    }

    private static bool IsAnyOn(IInvocationOperation invocation, IOperation? collection)
    {
        if (invocation.TargetMethod.Name != "Any")
            return false;

        if (invocation.Instance is { } instance)
            return invocation.Arguments.IsEmpty && SameReference(instance, collection);

        return invocation.TargetMethod.IsExtensionMethod
               && invocation.Arguments.Length == 1
               && SameReference(invocation.Arguments[0].Value, collection);
    }

    private static bool TestsCollectionSize(IPatternOperation pattern)
    {
        return pattern switch
        {
            IListPatternOperation => true,
            IRecursivePatternOperation recursive => recursive.PropertySubpatterns.Any(property =>
                property.Member is IPropertyReferenceOperation member && IsSizeProperty(member.Property.Name)),
            INegatedPatternOperation negated => TestsCollectionSize(negated.Pattern),
            IBinaryPatternOperation binary => TestsCollectionSize(binary.LeftPattern) || TestsCollectionSize(binary.RightPattern),
            _ => false
        };
    }

    private static bool IsSizeProperty(string name)
    {
        return name is "Length" or "Count" or "IsEmpty" or "IsDefaultOrEmpty";
    }

    private static bool TestsForNull(IPatternOperation pattern)
    {
        return pattern switch
        {
            IConstantPatternOperation constant => IsNull(constant.Value),
            INegatedPatternOperation negated => TestsForNull(negated.Pattern),
            IBinaryPatternOperation binary => TestsForNull(binary.LeftPattern) || TestsForNull(binary.RightPattern),
            IRecursivePatternOperation { DeconstructionSubpatterns.IsEmpty: true, PropertySubpatterns.IsEmpty: true } => true,
            ITypePatternOperation or IDeclarationPatternOperation { MatchesNull: false } => true,
            _ => false
        };
    }

    private static bool IsNull(IOperation value)
    {
        return value.ConstantValue is { HasValue: true, Value: null };
    }

    private static bool SameReference(IOperation? left, IOperation? right)
    {
        return (Unwrap(left), Unwrap(right)) switch
        {
            (ILocalReferenceOperation a, ILocalReferenceOperation b)
                => SymbolEqualityComparer.Default.Equals(a.Local, b.Local),
            (IParameterReferenceOperation a, IParameterReferenceOperation b)
                => SymbolEqualityComparer.Default.Equals(a.Parameter, b.Parameter),
            (IFieldReferenceOperation a, IFieldReferenceOperation b)
                => SymbolEqualityComparer.Default.Equals(a.Field, b.Field) && SameReference(a.Instance, b.Instance),
            (IPropertyReferenceOperation { Arguments.IsEmpty: true } a, IPropertyReferenceOperation { Arguments.IsEmpty: true } b)
                => SymbolEqualityComparer.Default.Equals(a.Property, b.Property) && SameReference(a.Instance, b.Instance),
            (IInstanceReferenceOperation a, IInstanceReferenceOperation b) => a.ReferenceKind == b.ReferenceKind,
            (null, null) => true,
            _ => false
        };
    }

    private static IOperation? Unwrap(IOperation? operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation { OperatorMethod: null } conversion:
                    operation = conversion.Operand;
                    break;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    break;
                case IConditionalAccessInstanceOperation receiver:
                    operation = ConditionalReceiver(receiver);
                    break;
                default:
                    return operation;
            }
        }
    }

    private static IOperation? ConditionalReceiver(IOperation receiver)
    {
        for (var child = receiver; child.Parent is { } parent; child = parent)
            if (parent is IConditionalAccessOperation access && child == access.WhenNotNull)
                return access.Operation;

        return null;
    }
}
