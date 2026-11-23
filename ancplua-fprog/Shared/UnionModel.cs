using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    public static bool AlwaysConvertsTo(Compilation compilation, ITypeSymbol source, ITypeSymbol target)
    {
        return compilation.ClassifyConversion(source, target) is
            { IsIdentity: true } or { IsImplicit: true, IsReference: true } or { IsImplicit: true, IsBoxing: true };
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
