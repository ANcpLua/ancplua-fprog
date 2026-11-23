using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>Compiler-native unions, their values and pattern coverage.</summary>
internal static class UnionModel
{
    public static bool IsUnion(ITypeSymbol? type)
    {
        return type is { IsUnion: true } or { TypeKind: TypeKind.Class, IsClosed: true };
    }

    public static INamedTypeSymbol? GetUnionType(ITypeSymbol? type)
    {
        return type switch
        {
            INamedTypeSymbol named when IsUnion(named) => named,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                => GetUnionType(nullable.TypeArguments[0]),
            ITypeParameterSymbol parameter => GetClosedConstraint(parameter, null),
            _ => null
        };
    }

    private static INamedTypeSymbol? GetClosedConstraint(
        ITypeParameterSymbol parameter, HashSet<ITypeParameterSymbol>? visited)
    {
        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is INamedTypeSymbol { TypeKind: TypeKind.Class, IsClosed: true } closed)
                return closed;

            if (constraint is ITypeParameterSymbol other)
            {
                visited ??= new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default) { parameter };
                if (visited.Add(other) && GetClosedConstraint(other, visited) is { } union)
                    return union;
            }
        }

        return null;
    }

    public static INamedTypeSymbol? GetUnionOfExpression(IOperation value, Compilation compilation)
    {
        var union = GetUnionType(value.Type);
        while (GetUpcastOperand(value, compilation) is { } operand)
        {
            value = operand;
            union = GetUnionType(value.Type) ?? union;
        }

        return union;
    }

    /// <summary>Finds the original value through parentheses, upcasts and boxing.</summary>
    public static IOperation ValueBeforeUpcast(IOperation value, Compilation compilation)
    {
        while (GetUpcastOperand(value, compilation) is { } operand)
            value = operand;

        return value;
    }

    private static IOperation? GetUpcastOperand(IOperation value, Compilation compilation)
    {
        return value switch
        {
            IParenthesizedOperation parenthesized => parenthesized.Operand,
            IConversionOperation { Type: { } target, Operand.Type: { } source } conversion
                when AlwaysConvertsTo(compilation, source, target) => conversion.Operand,
            _ => null
        };
    }

    public static ImmutableArray<ITypeSymbol> GetCases(INamedTypeSymbol union, CancellationToken cancellationToken)
    {
        if (union.IsUnion)
            return [.. union.InstanceConstructors
                .Where(static constructor => constructor is
                    { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 })
                .Select(static constructor => constructor.Parameters[0].Type)];

        return union.IsClosed
            ? [.. union.GetClosedDerivedTypeInfo(cancellationToken).ClosedDerivedTypes]
            : [];
    }

    public static IEnumerable<ITypeSymbol> GetLeafCases(INamedTypeSymbol union, CancellationToken cancellationToken)
    {
        foreach (var @case in GetCases(union, cancellationToken))
        {
            if (@case is INamedTypeSymbol { IsClosed: true } closed)
            {
                foreach (var leaf in GetLeafCases(closed, cancellationToken))
                    yield return leaf;
            }
            else
            {
                yield return @case;
            }
        }
    }

    public static bool AlwaysConvertsTo(Compilation compilation, ITypeSymbol source, ITypeSymbol? target)
    {
        return target is not null && compilation.ClassifyConversion(source, target) is
            { IsIdentity: true } or { IsImplicit: true, IsReference: true } or { IsImplicit: true, IsBoxing: true };
    }

    public static INamedTypeSymbol? GetTupleWithUnionElement(IOperation value, Compilation compilation)
    {
        var original = ValueBeforeUpcast(value, compilation);
        if (original.Type is not INamedTypeSymbol { IsTupleType: true } tuple)
            return null;

        return tuple.TupleElements.Any(static element => GetUnionType(element.Type) is not null)
               || original is ITupleOperation tupleValue
               && tupleValue.Elements.Any(element => GetUnionOfExpression(element, compilation) is not null)
            ? tuple
            : null;
    }

    public static IEnumerable<SyntaxNode> GetCatchAlls(
        Compilation compilation, IOperation value, IPatternOperation pattern, CancellationToken cancellationToken)
    {
        return GetCatchAlls(compilation, pattern, value, GetTupleWithUnionElement(value, compilation), cancellationToken);
    }

    private static IEnumerable<SyntaxNode> GetCatchAlls(Compilation compilation, IPatternOperation pattern,
        IOperation? input, ITypeSymbol? tuple, CancellationToken cancellationToken)
    {
        var union = (input is not null ? GetUnionOfExpression(input, compilation) : null)
                    ?? GetUnionType(pattern.InputType);
        if (union is not null || tuple is not null)
        {
            var syntax = GetPatternSyntax(pattern);
            if (syntax is VarPatternSyntax || IsCatchAll(compilation, pattern, union, tuple, cancellationToken))
                yield return syntax;
        }

        var tupleInput = pattern is IRecursivePatternOperation && input is not null
            ? ValueBeforeUpcast(input, compilation) as ITupleOperation
            : null;
        var position = 0;
        foreach (var child in pattern.ChildOperations)
        {
            if (child is IPropertySubpatternOperation property)
            {
                foreach (var catchAll in GetCatchAlls(compilation, property.Pattern, null, null, cancellationToken))
                    yield return catchAll;
            }
            else if (child is IPatternOperation subpattern)
            {
                // Logical subpatterns keep the governing value unless the left side of an and narrows it.
                var sameInput = pattern is IBinaryPatternOperation or INegatedPatternOperation
                                && SymbolEqualityComparer.Default.Equals(pattern.InputType, subpattern.InputType);
                var subpatternInput = sameInput
                    ? input
                    : tupleInput is not null && position < tupleInput.Elements.Length
                        ? tupleInput.Elements[position]
                        : null;
                foreach (var catchAll in GetCatchAlls(compilation, subpattern, subpatternInput,
                             sameInput ? tuple : null, cancellationToken))
                    yield return catchAll;
                position++;
            }
        }
    }

    private static bool IsCatchAll(Compilation compilation, IPatternOperation pattern, INamedTypeSymbol? union,
        ITypeSymbol? tuple, CancellationToken cancellationToken)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }:
            case INegatedPatternOperation:
            case IRecursivePatternOperation
                { Syntax: RecursivePatternSyntax { Type: null, PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0 } }:
                return true;
        }

        var tested = pattern switch
        {
            ITypePatternOperation type => type.MatchedType,
            IDeclarationPatternOperation declaration => declaration.MatchedType,
            IRecursivePatternOperation
                { Syntax: RecursivePatternSyntax { PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0 } } recursive
                => recursive.MatchedType,
            _ => null
        };

        if (tested is null)
            return false;

        if (union is null)
            return tuple is not null && AlwaysConvertsTo(compilation, tuple, tested);

        if (AlwaysConvertsTo(compilation, union, tested)
            || AlwaysConvertsTo(compilation, pattern.InputType, tested))
            return true;

        var cases = GetCases(union, cancellationToken);
        if (cases.Length == 1 && SymbolEqualityComparer.Default.Equals(cases[0], tested))
            return false;

        var leaves = GetLeafCases(union, cancellationToken).ToArray();
        return leaves.Length > 0
               && !(leaves.Length == 1 && SymbolEqualityComparer.Default.Equals(leaves[0], tested))
               && leaves.All(@case => AlwaysConvertsTo(compilation, @case, tested));
    }

    private static SyntaxNode GetPatternSyntax(IPatternOperation pattern)
    {
        var syntax = pattern.Syntax;
        while (UnwrapPattern(syntax) is { } inner)
            syntax = inner;

        return syntax is VariableDesignationSyntax { Parent: VarPatternSyntax variable } ? variable : syntax;
    }

    private static SyntaxNode? UnwrapPattern(SyntaxNode syntax)
    {
        return syntax switch
        {
            ParenthesizedPatternSyntax parenthesized => parenthesized.Pattern,
            ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
            ConstantPatternSyntax constant => constant.Expression,
            _ => null
        };
    }

    public static bool CoversCase(Compilation compilation, IPatternOperation pattern, ITypeSymbol caseType)
    {
        return pattern switch
        {
            ITypePatternOperation type => AlwaysConvertsTo(compilation, caseType, type.MatchedType),
            IDeclarationPatternOperation { MatchedType: { } type }
                => AlwaysConvertsTo(compilation, caseType, type),
            IRecursivePatternOperation recursive
                => AlwaysConvertsTo(compilation, caseType, recursive.MatchedType)
                   && recursive.DeconstructionSubpatterns.All(IsUnconditionalSubpattern)
                   && recursive.PropertySubpatterns.All(static property => IsUnconditionalSubpattern(property.Pattern)),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either
                => CoversCase(compilation, either.LeftPattern, caseType)
                   || CoversCase(compilation, either.RightPattern, caseType),
            _ => false
        };
    }

    private static bool IsUnconditionalSubpattern(IPatternOperation pattern)
    {
        return pattern is IDiscardPatternOperation or IDeclarationPatternOperation { MatchesNull: true };
    }
}
