using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

// Which cases the arms of a switch expression handle. Observed with Roslyn 5.9.0:
// - A parenthesized pattern has no operation of its own: (Circle c) is the declaration pattern Circle c.
// - The type pattern Shape.Circle parses as a constant pattern; only its operation says it is a type test.
// - { }, { } x, (a, b) and var (a, b) are recursive patterns whose MatchedType is the input type; for an
//   input of type int? it is int. On the right of "and" the input type is the narrowed type of the left.
// - IPatternOperation.InputType has lost the nullable annotation of a reference type.
// - DeconstructSymbol is the Deconstruct method (an extension method in its static form, with the receiver
//   as first parameter; a member of an extension block without it), null for a tuple, and the type ITuple
//   when the positional pattern is a length test at run time.
// - In { A.B: p } the pattern of A is an implicit recursive pattern with the subpattern B: p.
internal sealed partial class UnionModel
{
    /// <summary>How the pattern of one arm relates to one case. The order matters: a later value is stronger.</summary>
    private enum ArmMatch
    {
        /// <summary>Some non-null value of the case does not match.</summary>
        Partial,

        /// <summary>Every non-null value of the case matches, but no type test names the case.</summary>
        CatchAll,

        /// <summary>Every non-null value of the case matches, and that rests on a type test that names the case.</summary>
        Own
    }

    /// <summary>
    /// Says which cases the arms of <paramref name="switchExpression"/> handle. Returns
    /// <see langword="false"/> when the switched value is not a union value (<see cref="GetUnionType"/>
    /// of its static type); <paramref name="coverage"/> is then empty.
    /// <para>
    /// This is the one definition of "handles every case" for FP0502 and FPS001. An arm handles a case
    /// when it has no <c>when</c> clause, tests for the case's own type with the type arguments the
    /// value has, and matches every non-null value of the case: <c>Circle</c>, <c>Circle c</c>,
    /// <c>Circle { }</c>, <c>Circle(var r)</c>, <c>Circle { Radius: var r }</c>, <c>Circle or Square</c>,
    /// <c>Circle and var c</c>; not <c>Circle { Radius: > 0 }</c>, and not <c>Result&lt;int&gt;.Ok</c> on a
    /// <c>Result&lt;T&gt;</c>. A subpattern is judged against the declared type of its input:
    /// <c>Leaf(string name)</c> handles <c>Leaf</c> when the component is a <c>string</c>, not when it is
    /// a <c>string?</c>; a type parameter without <c>?</c> counts as not null. Two arms that are partial
    /// are never combined, and <c>not Circle</c> handles nothing.
    /// </para>
    /// </summary>
    public bool TryGetCoverage(ISwitchExpressionOperation switchExpression, out UnionSwitchCoverage coverage)
    {
        if (switchExpression.Value.Type is not { } valueType || GetUnionType(valueType) is not { } unionType)
        {
            coverage = default;
            return false;
        }

        var cases = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var casesWithoutOwnArm = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var unhandledCases = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var definition in GetAllCases(unionType.OriginalDefinition))
        {
            if (!TryBind(definition, unionType, out var bindings) || !CanBeNamedAt(switchExpression, definition))
                continue;

            cases.Add(definition);
            var match = GetStrongestMatch(switchExpression, new CaseOfValue(valueType, definition, bindings));
            if (match == ArmMatch.Own)
                continue;

            casesWithoutOwnArm.Add(definition);
            if (match == ArmMatch.Partial)
                unhandledCases.Add(definition);
        }

        coverage = new UnionSwitchCoverage(
            unionType.OriginalDefinition,
            cases.ToImmutable(),
            casesWithoutOwnArm.ToImmutable(),
            unhandledCases.ToImmutable());
        return true;
    }

    /// <summary>
    /// False when the code at the switch cannot name the case: a private nested case seen from outside,
    /// an internal case of another assembly, or a file-local case of another file (which
    /// <see cref="SemanticModel.IsAccessible"/> calls accessible).
    /// </summary>
    private static bool CanBeNamedAt(ISwitchExpressionOperation switchExpression, INamedTypeSymbol definition)
    {
        if (switchExpression.SemanticModel is not { } semanticModel)
            return true;

        var syntax = switchExpression.Syntax;
        if (!semanticModel.IsAccessible(syntax.SpanStart, definition))
            return false;

        if (!definition.IsFileLocal)
            return true;

        foreach (var reference in definition.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == syntax.SyntaxTree)
                return true;
        }

        return false;
    }

    /// <summary>The strongest match of an arm without a <c>when</c> clause. An arm with one never counts.</summary>
    private ArmMatch GetStrongestMatch(ISwitchExpressionOperation switchExpression, CaseOfValue target)
    {
        var strongest = ArmMatch.Partial;
        foreach (var arm in switchExpression.Arms)
        {
            if (arm.Guard is null)
                strongest = Stronger(strongest, Classify(arm.Pattern, target));
        }

        return strongest;
    }

    private static ArmMatch Stronger(ArmMatch left, ArmMatch right) => left > right ? left : right;

    private ArmMatch Classify(IPatternOperation pattern, CaseOfValue target)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }: // var x
                return ArmMatch.CatchAll;
            case IDeclarationPatternOperation { MatchedType: { } declaredType }:
                return ClassifyTypeTest(declaredType, target);
            case ITypePatternOperation typePattern:
                return ClassifyTypeTest(typePattern.MatchedType, target);
            case IRecursivePatternOperation recursive:
                var typeTest = ClassifyTypeTest(recursive.MatchedType, target);
                return typeTest != ArmMatch.Partial && HasIrrefutableSubpatterns(recursive) ? typeTest : ArmMatch.Partial;
            case INegatedPatternOperation negated:
                return IsNullConstant(negated.Pattern) ? ArmMatch.CatchAll : ArmMatch.Partial;
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either:
                return Stronger(Classify(either.LeftPattern, target), Classify(either.RightPattern, target));
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both:
                var left = Classify(both.LeftPattern, target);
                var right = Classify(both.RightPattern, target);
                return left == ArmMatch.Partial || right == ArmMatch.Partial ? ArmMatch.Partial : Stronger(left, right);
            default: // constant, relational, list and slice patterns
                return ArmMatch.Partial;
        }
    }

    /// <summary>Classifies the test "the value is a <paramref name="testedType"/>".</summary>
    private ArmMatch ClassifyTypeTest(ITypeSymbol testedType, CaseOfValue target)
    {
        var definition = target.Definition;
        if (SymbolEqualityComparer.Default.Equals(testedType.OriginalDefinition, definition))
            return Agrees(definition, testedType, target.Bindings) ? ArmMatch.Own : ArmMatch.Partial;

        // The union type itself, object, a base class or an interface of the union, the type parameter of the value.
        if (AlwaysConvertsTo(target.ValueType, testedType))
            return ArmMatch.CatchAll;

        for (var baseType = definition.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (Agrees(baseType, testedType, target.Bindings))
                return ArmMatch.CatchAll;
        }

        foreach (var implemented in definition.AllInterfaces)
        {
            if (Agrees(implemented, testedType, target.Bindings))
                return ArmMatch.CatchAll;
        }

        return ArmMatch.Partial;
    }

    /// <summary>
    /// True when every positional and every property subpattern of <paramref name="pattern"/> matches
    /// every value its input can hold.
    /// </summary>
    private bool HasIrrefutableSubpatterns(IRecursivePatternOperation pattern)
    {
        var positional = pattern.DeconstructionSubpatterns;
        for (var index = 0; index < positional.Length; index++)
        {
            if (GetPositionalInputType(pattern, index) is not { } inputType || !IsIrrefutable(positional[index], inputType))
                return false;
        }

        foreach (var subpattern in pattern.PropertySubpatterns)
        {
            if (!IsIrrefutable(subpattern.Pattern, GetMemberInputType(subpattern)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// The declared type of the input of the positional subpattern at <paramref name="index"/>:
    /// the type of the matching <c>Deconstruct</c> parameter or tuple element (the tuple type of a
    /// pattern keeps the annotations of its elements). <see langword="null"/> when the pattern
    /// deconstructs neither way (<c>ITuple</c> at run time, or erroneous code).
    /// </summary>
    private static ITypeSymbol? GetPositionalInputType(IRecursivePatternOperation pattern, int index)
    {
        var subpatterns = pattern.DeconstructionSubpatterns;
        switch (pattern.DeconstructSymbol)
        {
            case IMethodSymbol deconstruct:
                // An extension method Deconstruct(this Circle circle, out double radius) has the receiver first.
                var parameters = deconstruct.Parameters;
                var receivers = parameters.Length - subpatterns.Length;
                return receivers is 0 or 1 ? parameters[index + receivers].Type : subpatterns[index].InputType;
            case null when pattern.MatchedType is INamedTypeSymbol { IsTupleType: true, TupleElements: var elements }
                           && elements.Length == subpatterns.Length:
                return elements[index].Type;
            default:
                return null;
        }
    }

    /// <summary>The declared type of the property or field that <paramref name="subpattern"/> reads.</summary>
    private static ITypeSymbol GetMemberInputType(IPropertySubpatternOperation subpattern)
        => subpattern.Member switch
        {
            IPropertyReferenceOperation property => property.Property.Type,
            IFieldReferenceOperation field => field.Field.Type,
            _ => subpattern.Pattern.InputType
        };

    /// <summary>
    /// True when <paramref name="pattern"/> matches every value that an input declared as
    /// <paramref name="declaredInput"/> can hold. The declared type is needed because
    /// <see cref="IPatternOperation.InputType"/> does not tell <c>string</c> from <c>string?</c>.
    /// </summary>
    private bool IsIrrefutable(IPatternOperation pattern, ITypeSymbol declaredInput)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }: // var x
                return true;
            case IDeclarationPatternOperation { MatchedType: { } declaredType }:
                return AlwaysMatches(declaredInput, declaredType);
            case ITypePatternOperation typePattern:
                return AlwaysMatches(declaredInput, typePattern.MatchedType);
            case IRecursivePatternOperation recursive:
                return AlwaysMatches(declaredInput, recursive.MatchedType) && HasIrrefutableSubpatterns(recursive);
            case INegatedPatternOperation negated:
                return IsNullConstant(negated.Pattern) && CannotBeNull(declaredInput);
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either:
                return IsIrrefutable(either.LeftPattern, declaredInput) || IsIrrefutable(either.RightPattern, declaredInput);
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both:
                return IsIrrefutable(both.LeftPattern, declaredInput) && IsIrrefutable(both.RightPattern, declaredInput);
            default: // constant, relational, list and slice patterns
                return false;
        }
    }

    /// <summary>
    /// True when a test for <paramref name="testedType"/> matches every value of an input declared as
    /// <paramref name="declaredInput"/>.
    /// </summary>
    private bool AlwaysMatches(ITypeSymbol declaredInput, ITypeSymbol testedType)
        => CannotBeNull(declaredInput) && AlwaysConvertsTo(declaredInput, testedType);

    /// <summary>
    /// True when every non-null value of <paramref name="source"/> is a <paramref name="destination"/>:
    /// the conversion is an identity, an implicit reference or a boxing conversion.
    /// </summary>
    private bool AlwaysConvertsTo(ITypeSymbol source, ITypeSymbol destination)
    {
        var conversion = _compilation.ClassifyConversion(source, destination);
        return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing));
    }

    /// <summary>
    /// True unless the type is annotated <c>?</c> or is a <c>Nullable&lt;T&gt;</c>. A declared <c>int?</c>
    /// is annotated too; one that arrives as an <see cref="IPatternOperation.InputType"/> is not.
    /// </summary>
    private static bool CannotBeNull(ITypeSymbol declaredType)
        => declaredType.NullableAnnotation != NullableAnnotation.Annotated
           && declaredType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

    private static bool IsNullConstant(IPatternOperation pattern)
        => pattern is IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } };

    /// <summary>
    /// One case of the union of a switched value: the static type of the value, the case definition, and
    /// what <see cref="TryBind"/> bound the type parameters of the case to.
    /// </summary>
    private readonly struct CaseOfValue(
        ITypeSymbol valueType,
        INamedTypeSymbol definition,
        Dictionary<ITypeParameterSymbol, ITypeSymbol>? bindings)
    {
        public ITypeSymbol ValueType { get; } = valueType;

        public INamedTypeSymbol Definition { get; } = definition;

        public Dictionary<ITypeParameterSymbol, ITypeSymbol>? Bindings { get; } = bindings;
    }
}
