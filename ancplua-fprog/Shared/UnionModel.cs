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
internal sealed class UnionModel(Compilation compilation)
{
    private readonly Dictionary<INamedTypeSymbol, CaseSet> _casesByType = new(SymbolEqualityComparer.Default);

    public INamedTypeSymbol? GetUnionType(ITypeSymbol? type, CancellationToken cancellationToken)
    {
        return type switch
        {
            INamedTypeSymbol { IsUnion: true } union => union,
            INamedTypeSymbol { TypeKind: TypeKind.Class, IsClosed: true } closed
                when GetCases(closed, cancellationToken).IsComplete => closed,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                => GetUnionType(nullable.TypeArguments[0], cancellationToken),
            ITypeParameterSymbol parameter => GetClosedConstraint(parameter, null, cancellationToken),
            _ => null
        };
    }

    private INamedTypeSymbol? GetClosedConstraint(
        ITypeParameterSymbol parameter, HashSet<ITypeParameterSymbol>? visited, CancellationToken cancellationToken)
    {
        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is INamedTypeSymbol { TypeKind: TypeKind.Class, IsClosed: true } closed
                && GetCases(closed, cancellationToken).IsComplete)
                return closed;

            if (constraint is ITypeParameterSymbol other)
            {
                visited ??= new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default) { parameter };
                if (visited.Add(other) && GetClosedConstraint(other, visited, cancellationToken) is { } union)
                    return union;
            }
        }

        return null;
    }

    public INamedTypeSymbol? GetUnionOfExpression(IOperation value, CancellationToken cancellationToken)
    {
        var union = GetUnionType(value.Type, cancellationToken);
        while (GetUpcastOperand(value) is { } operand)
        {
            value = operand;
            union = GetUnionType(value.Type, cancellationToken) ?? union;
        }

        return union;
    }

    /// <summary>Finds the original value through parentheses, upcasts and boxing.</summary>
    public IOperation ValueBeforeUpcast(IOperation value)
    {
        while (GetUpcastOperand(value) is { } operand)
            value = operand;

        return value;
    }

    private IOperation? GetUpcastOperand(IOperation value)
    {
        return value switch
        {
            IParenthesizedOperation parenthesized => parenthesized.Operand,
            IConversionOperation { Type: { } target, Operand.Type: { } source } conversion
                when AlwaysConvertsTo(source, target) => conversion.Operand,
            _ => null
        };
    }

    public CaseSet GetCases(INamedTypeSymbol union, CancellationToken cancellationToken)
    {
        lock (_casesByType)
        {
            if (_casesByType.TryGetValue(union, out var known))
                return known;

            var isComplete = true;
            var direct = union.IsUnion ? union.UnionCaseTypes : ClosedCases(union, cancellationToken, out isComplete);
            var cases = new CaseSet(direct, isComplete ? ExpandClosedCases(direct, cancellationToken) : [], isComplete);
            _casesByType.Add(union, cases);
            return cases;
        }
    }

    private static ImmutableArray<ITypeSymbol> ClosedCases(
        INamedTypeSymbol closed, CancellationToken cancellationToken, out bool isComplete)
    {
        var derived = closed.GetClosedDerivedTypeInfo(cancellationToken);
        isComplete = derived.IsComplete;
        return [.. derived.ClosedDerivedTypes];
    }

    // A closed case stands for its direct subtypes, also when it is a hand-written union itself: what such a
    // case holds in Value is not a subtype of the type being matched. Each step goes to a derived class, so
    // the expansion ends.
    private static ImmutableArray<ITypeSymbol> ExpandClosedCases(
        ImmutableArray<ITypeSymbol> direct, CancellationToken cancellationToken)
    {
        if (!direct.Any(static @case => @case.IsClosed))
            return direct;

        // A union can name a closed class and one of its subtypes; each leaf counts once.
        var leaves = ImmutableArray.CreateBuilder<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var @case in direct)
        {
            if (@case is INamedTypeSymbol { TypeKind: TypeKind.Class, IsClosed: true } closed
                && ClosedCases(closed, cancellationToken, out var isComplete) is var nested && isComplete)
            {
                foreach (var leaf in ExpandClosedCases(nested, cancellationToken))
                    if (seen.Add(leaf))
                        leaves.Add(leaf);
            }
            else if (seen.Add(@case))
            {
                leaves.Add(@case);
            }
        }

        return leaves.ToImmutable();
    }

    public bool AlwaysConvertsTo(ITypeSymbol source, ITypeSymbol? target)
    {
        return target is not null && compilation.ClassifyConversion(source, target) is
            { IsIdentity: true } or { IsImplicit: true, IsReference: true } or { IsImplicit: true, IsBoxing: true };
    }

    public INamedTypeSymbol? GetTupleWithUnionElement(IOperation value, CancellationToken cancellationToken)
    {
        var original = ValueBeforeUpcast(value);
        if (original.Type is not INamedTypeSymbol { IsTupleType: true } tuple)
            return null;

        return tuple.TupleElements.Any(element => GetUnionType(element.Type, cancellationToken) is not null)
               || original is ITupleOperation tupleValue
               && tupleValue.Elements.Any(element => GetUnionOfExpression(element, cancellationToken) is not null)
            ? tuple
            : null;
    }

    public IEnumerable<SyntaxNode> GetCatchAlls(
        IOperation value, IPatternOperation pattern, CancellationToken cancellationToken)
    {
        return GetCatchAlls(pattern, value, GetTupleWithUnionElement(value, cancellationToken), false, cancellationToken);
    }

    /// <summary>
    ///     True when the pattern is a catch-all for the switched value itself. A catch-all on one of its
    ///     properties or positions says nothing about the cases of the value.
    /// </summary>
    public bool CatchesSwitchedValue(IOperation value, IPatternOperation pattern, CancellationToken cancellationToken)
    {
        return GetCatchAlls(pattern, value, GetTupleWithUnionElement(value, cancellationToken), true, cancellationToken)
            .Any();
    }

    private IEnumerable<SyntaxNode> GetCatchAlls(IPatternOperation pattern,
        IOperation? input, ITypeSymbol? tuple, bool switchedValueOnly, CancellationToken cancellationToken)
    {
        var union = (input is not null ? GetUnionOfExpression(input, cancellationToken) : null)
                    ?? GetUnionType(pattern.InputType, cancellationToken);
        if (union is not null || tuple is not null)
        {
            var syntax = GetPatternSyntax(pattern);
            if (syntax is VarPatternSyntax || IsCatchAll(pattern, union, tuple, cancellationToken))
                yield return syntax;
        }

        var tupleInput = pattern is IRecursivePatternOperation && input is not null
            ? ValueBeforeUpcast(input) as ITupleOperation
            : null;
        var position = 0;
        foreach (var child in pattern.ChildOperations)
        {
            if (child is IPropertySubpatternOperation property)
            {
                if (switchedValueOnly)
                    continue;

                foreach (var catchAll in GetCatchAlls(property.Pattern, null, null, false, cancellationToken))
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
                if (sameInput || !switchedValueOnly)
                    foreach (var catchAll in GetCatchAlls(subpattern, subpatternInput,
                                 sameInput ? tuple : null, switchedValueOnly, cancellationToken))
                        yield return catchAll;
                position++;
            }
        }
    }

    private bool IsCatchAll(IPatternOperation pattern, INamedTypeSymbol? union,
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
            return tuple is not null && AlwaysConvertsTo(tuple, tested);

        if (AlwaysConvertsTo(union, tested)
            || AlwaysConvertsTo(pattern.InputType, tested))
            return true;

        var cases = GetCases(union, cancellationToken);
        if (cases.Direct.Length == 1 && SymbolEqualityComparer.Default.Equals(cases.Direct[0], tested))
            return false;

        var leaves = cases.Leaves;
        return leaves.Length > 0
               && !(leaves.Length == 1 && SymbolEqualityComparer.Default.Equals(leaves[0], tested))
               && leaves.All(@case => AlwaysConvertsTo(@case, tested));
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

    public bool CoversCase(IPatternOperation pattern, ITypeSymbol caseType)
    {
        return pattern switch
        {
            ITypePatternOperation type => AlwaysConvertsTo(caseType, type.MatchedType),
            IDeclarationPatternOperation { MatchedType: { } type }
                => AlwaysConvertsTo(caseType, type),
            IRecursivePatternOperation recursive
                => AlwaysConvertsTo(caseType, recursive.MatchedType)
                   && recursive.DeconstructionSubpatterns.All(IsUnconditionalSubpattern)
                   && recursive.PropertySubpatterns.All(static property => IsUnconditionalSubpattern(property.Pattern)),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either
                => CoversCase(either.LeftPattern, caseType)
                   || CoversCase(either.RightPattern, caseType),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both
                => CoversCase(both.LeftPattern, caseType)
                   && CoversCase(both.RightPattern, caseType),
            _ => false
        };
    }

    private static bool IsUnconditionalSubpattern(IPatternOperation pattern)
    {
        return pattern is IDiscardPatternOperation or IDeclarationPatternOperation { MatchesNull: true };
    }

    public sealed class CaseSet(ImmutableArray<ITypeSymbol> direct, ImmutableArray<ITypeSymbol> leaves, bool isComplete)
    {
        public ImmutableArray<ITypeSymbol> Direct { get; } = direct;
        public ImmutableArray<ITypeSymbol> Leaves { get; } = leaves;
        public bool IsComplete { get; } = isComplete;
    }
}
