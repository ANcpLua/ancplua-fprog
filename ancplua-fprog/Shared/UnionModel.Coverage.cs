using System;
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
// - The members a pattern reads belong to the type the arm names: Result<string>.Ok(string v) reads
//   Result<string>.Ok.Deconstruct(out string), also when the switched value is a Result<string?>.
// - A generic extension member is constructed without nullable annotations: Box<string?>.Full(string item)
//   reads Deconstruct<string>(this Box<string>.Full, out string) for Deconstruct<T>(this Box<T>.Full, out T).
internal sealed partial class UnionModel
{
    /// <summary>A type test that names the case and therefore matches every non-null value of it.</summary>
    private const ArmMatch OwnArm = ArmMatch.Named | ArmMatch.Covered | ArmMatch.Own;

    /// <summary>
    ///     Says which cases the arms of <paramref name="switchExpression" /> name and handle. Returns
    ///     <see langword="false" /> when the switched value is not a union value (<see cref="GetUnionType" />
    ///     of its static type); <paramref name="coverage" /> is then empty.
    ///     <para>
    ///         An arm with a <c>when</c> clause never counts. An arm names a case when a type test in it tests
    ///         for the case's own type with the type arguments the value has, alone or as a side of <c>or</c> or
    ///         <c>and</c>: <c>Circle</c>, <c>Circle c</c>, <c>Circle { Radius: > 0 }</c>, <c>Circle or Square</c>;
    ///         not <c>Result&lt;int&gt;.Ok</c> on a <c>Result&lt;T&gt;</c>, not <c>not Circle</c>, not a catch-all.
    ///         An arm is the case's own arm when it names the case and matches every non-null value of it:
    ///         <c>Circle</c>, <c>Circle c</c>, <c>Circle { }</c>, <c>Circle(var r)</c>, <c>Circle { Radius: var r }</c>,
    ///         <c>Circle and var c</c>; not <c>Circle { Radius: > 0 }</c>. Arms are never combined: two arms that
    ///         each reject some value of a case are not its own arm together.
    ///     </para>
    ///     <para>
    ///         A subpattern is judged against the declared type of its input as the value sees it:
    ///         <c>Leaf(string name)</c> matches every <c>Leaf</c> when the component is a <c>string</c>, not when
    ///         it is a <c>string?</c>; and <c>Result&lt;string&gt;.Ok(string v)</c> does not match every <c>Ok</c>
    ///         of a <c>Result&lt;string?&gt;</c>. A type parameter without <c>?</c> and a type from code without
    ///         nullable annotations count as not null. A reference-typed input that a generic extension member
    ///         supplies for one of its type parameters (<c>out T item</c> of an extension
    ///         <c>Deconstruct&lt;T&gt;</c>) counts as nullable: Roslyn constructs such a member without the
    ///         <c>?</c> of the value. So does one that an extension member supplies whose receiver type lacks a
    ///         <c>?</c> of the value (<c>this Box&lt;string&gt;.Full</c> on a <c>Box&lt;string?&gt;</c>).
    ///     </para>
    /// </summary>
    public bool TryGetCoverage(ISwitchExpressionOperation switchExpression, out UnionSwitchCoverage coverage)
    {
        if (switchExpression.Value.Type is not { } valueType || GetUnionType(valueType) is not { } unionType)
        {
            coverage = default;
            return false;
        }

        var cases = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var casesNotNamed = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var casesWithoutOwnArm = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var unhandledCases = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var armsCanNameEveryValue = true;
        foreach (var definition in GetAllCases(unionType.OriginalDefinition))
        {
            if (!TryBind(definition, unionType, out var bindings))
                continue;

            if (!CanBeNamedAt(switchExpression, definition))
            {
                // No arm can be asked for, but a value of this case reaches none.
                armsCanNameEveryValue = false;
                continue;
            }

            cases.Add(definition);
            armsCanNameEveryValue &= BindsEveryTypeParameter(definition, bindings);
            var match = Classify(switchExpression, new CaseOfValue(valueType, definition, bindings));
            if ((match & ArmMatch.Named) == 0)
                casesNotNamed.Add(definition);

            if ((match & ArmMatch.Own) == 0)
                casesWithoutOwnArm.Add(definition);

            if ((match & ArmMatch.Covered) == 0)
                unhandledCases.Add(definition);
        }

        coverage = new UnionSwitchCoverage(
            unionType.OriginalDefinition,
            cases.ToImmutable(),
            casesNotNamed.ToImmutable(),
            casesWithoutOwnArm.ToImmutable(),
            unhandledCases.ToImmutable(),
            armsCanNameEveryValue);
        return true;
    }

    /// <summary>
    ///     False when the code at the switch cannot name the case: a private nested case seen from outside,
    ///     an internal case of another assembly, or a case that is, or is nested in, a file-local type of
    ///     another file (which <see cref="SemanticModel.IsAccessible" /> calls accessible).
    /// </summary>
    private static bool CanBeNamedAt(ISwitchExpressionOperation switchExpression, INamedTypeSymbol definition)
    {
        if (switchExpression.SemanticModel is not { } semanticModel)
            return true;

        var syntax = switchExpression.Syntax;
        if (!semanticModel.IsAccessible(syntax.SpanStart, definition))
            return false;

        for (var type = definition; type is not null; type = type.ContainingType)
            if (type.IsFileLocal && !IsDeclaredIn(type, syntax.SyntaxTree))
                return false;

        return true;
    }

    private static bool IsDeclaredIn(INamedTypeSymbol type, SyntaxTree tree)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
            if (reference.SyntaxTree == tree)
                return true;

        return false;
    }

    /// <summary>What the arms without a <c>when</c> clause do with the case. An arm with one never counts.</summary>
    private ArmMatch Classify(ISwitchExpressionOperation switchExpression, CaseOfValue target)
    {
        var match = ArmMatch.None;
        foreach (var arm in switchExpression.Arms)
            if (arm.Guard is null)
                match |= Classify(arm.Pattern, target);

        return match;
    }

    private ArmMatch Classify(IPatternOperation pattern, CaseOfValue target)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }: // var x
                return ArmMatch.Covered;
            case IDeclarationPatternOperation { MatchedType: { } declaredType }:
                return ClassifyTypeTest(declaredType, target);
            case ITypePatternOperation typePattern:
                return ClassifyTypeTest(typePattern.MatchedType, target);
            case IRecursivePatternOperation recursive:
                // With a subpattern that can reject a value, the pattern still names the case.
                var typeTest = ClassifyTypeTest(recursive.MatchedType, target);
                return (typeTest & ArmMatch.Covered) == 0 ||
                       HasIrrefutableSubpatterns(recursive, GetInput(recursive, target))
                    ? typeTest
                    : typeTest & ArmMatch.Named;
            case INegatedPatternOperation negated:
                return IsNullConstant(negated.Pattern) || IsTestForAnotherCase(negated.Pattern, target)
                    ? ArmMatch.Covered
                    : ArmMatch.None;
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either:
                return Classify(either.LeftPattern, target) | Classify(either.RightPattern, target);
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both:
                var left = Classify(both.LeftPattern, target);
                var right = Classify(both.RightPattern, target);
                var named = (left | right) & ArmMatch.Named;
                return (left & right & ArmMatch.Covered) == 0
                    ? named
                    : named | ArmMatch.Covered | ((left | right) & ArmMatch.Own);
            default: // constant, relational, list and slice patterns
                return ArmMatch.None;
        }
    }

    /// <summary>Classifies the test "the value is a <paramref name="testedType" />".</summary>
    private ArmMatch ClassifyTypeTest(ITypeSymbol testedType, CaseOfValue target)
    {
        var definition = target.Definition;
        if (SymbolEqualityComparer.Default.Equals(testedType.OriginalDefinition, definition))
            return Agrees(definition, testedType, target.Bindings) ? OwnArm : ArmMatch.None;

        // The union type itself, object, a base class or an interface of the union, the type parameter of the value.
        if (AlwaysConvertsTo(target.ValueType, testedType))
            return ArmMatch.Covered;

        // A base type or interface built from a type parameter that the value leaves open stands for many
        // types (ITag<TTag> of Tagged<TTag> : Open<int>), and the tested type is one of them at most.
        var determined = BindsEveryTypeParameter(definition, target.Bindings);
        for (var baseType = definition.BaseType; baseType is not null; baseType = baseType.BaseType)
            if ((determined || !MentionsTypeParameter(baseType)) && Agrees(baseType, testedType, target.Bindings))
                return ArmMatch.Covered;

        foreach (var implemented in definition.AllInterfaces)
            if ((determined || !MentionsTypeParameter(implemented)) && Agrees(implemented, testedType, target.Bindings))
                return ArmMatch.Covered;

        return ArmMatch.None;
    }

    /// <summary>
    ///     True when <paramref name="pattern" /> is a type test for another case of the same union. No value
    ///     of this case passes it, whatever the type arguments, so its negation matches all of them:
    ///     <c>not Circle</c> matches every <c>Square</c>.
    /// </summary>
    private static bool IsTestForAnotherCase(IPatternOperation pattern, CaseOfValue target)
    {
        return pattern is ITypePatternOperation { MatchedType: var testedType }
               && !SymbolEqualityComparer.Default.Equals(testedType.OriginalDefinition, target.Definition)
               && SymbolEqualityComparer.Default.Equals(GetUnionOfCase(testedType), GetUnionOfCase(target.Definition));
    }

    /// <summary>
    ///     The input of a pattern of an arm that matches every value of the case: a value of the case as
    ///     the switched value constructs it. The arm may name a construction that lacks a <c>?</c> of it.
    /// </summary>
    private static DeclaredInput GetInput(IRecursivePatternOperation pattern, CaseOfValue target)
    {
        var caseOfValue = ConstructForValue(target.Definition, target.Bindings);
        var named = FindConstruction(caseOfValue, pattern.MatchedType.OriginalDefinition);
        return new DeclaredInput(caseOfValue, false, named is not null && LacksAnnotation(pattern.MatchedType, named));
    }

    /// <summary>
    ///     True when every positional and every property subpattern of <paramref name="pattern" /> matches
    ///     every value its input can hold. <paramref name="input" /> is the input of <paramref name="pattern" /> itself.
    /// </summary>
    private bool HasIrrefutableSubpatterns(IRecursivePatternOperation pattern, DeclaredInput input)
    {
        var positional = pattern.DeconstructionSubpatterns;
        for (var index = 0; index < positional.Length; index++)
            if (GetPositionalInput(pattern, input, index) is not { } component ||
                !IsIrrefutable(positional[index], component))
                return false;

        foreach (var subpattern in pattern.PropertySubpatterns)
            if (!IsIrrefutable(subpattern.Pattern, GetMemberInput(subpattern, input)))
                return false;

        return true;
    }

    /// <summary>
    ///     The input of the positional subpattern at <paramref name="index" />: the matching <c>Deconstruct</c>
    ///     parameter or tuple element. <see langword="null" /> when the pattern deconstructs neither way
    ///     (<c>ITuple</c> at run time, or erroneous code).
    /// </summary>
    private static DeclaredInput? GetPositionalInput(IRecursivePatternOperation pattern, DeclaredInput input, int index)
    {
        var subpatterns = pattern.DeconstructionSubpatterns;
        switch (pattern.DeconstructSymbol)
        {
            case IMethodSymbol deconstruct:
                // An extension method Deconstruct(this Circle circle, out double radius) has the receiver first.
                var parameters = deconstruct.Parameters;
                var receivers = parameters.Length - subpatterns.Length;
                if (receivers is not (0 or 1))
                    return input.WithoutMember(subpatterns[index].InputType, true);

                if (FindMember(input.Type, deconstruct) is { } seen && seen.Parameters.Length == parameters.Length)
                    return input.WithMember(seen.Parameters[index + receivers].Type);

                var declaredParameters = deconstruct.OriginalDefinition.Parameters;
                return input.WithoutMember(
                    parameters[index + receivers].Type,
                    declaredParameters.Length != parameters.Length
                    || MentionsTypeParameter(declaredParameters[index + receivers].Type)
                    || ReceiverLacksAnnotation(deconstruct, input.Type));
            case null when GetTupleElements(input.Type, subpatterns.Length) is { } declared:
                return input.WithMember(declared[index].Type);
            case null when GetTupleElements(pattern.MatchedType, subpatterns.Length) is { } matched:
                return input.WithoutMember(matched[index].Type, false);
            default:
                return null;
        }
    }

    private static ImmutableArray<IFieldSymbol>? GetTupleElements(ITypeSymbol type, int count)
    {
        return type is INamedTypeSymbol { IsTupleType: true, TupleElements: var elements } && elements.Length == count
            ? elements
            : null;
    }

    /// <summary>The input of a property subpattern: the property or field that it reads.</summary>
    private static DeclaredInput GetMemberInput(IPropertySubpatternOperation subpattern, DeclaredInput input)
    {
        switch (subpattern.Member)
        {
            case IPropertyReferenceOperation { Property: var property }:
                return FindMember(input.Type, property) is { } seenProperty
                    ? input.WithMember(seenProperty.Type)
                    : input.WithoutMember(
                        property.Type,
                        MentionsTypeParameter(property.OriginalDefinition.Type) ||
                        ReceiverLacksAnnotation(property, input.Type));
            case IFieldReferenceOperation { Field: var field }:
                return FindMember(input.Type, field) is { } seenField
                    ? input.WithMember(seenField.Type)
                    : input.WithoutMember(field.Type, MentionsTypeParameter(field.OriginalDefinition.Type));
            default:
                return input.WithoutMember(subpattern.Pattern.InputType, true);
        }
    }

    /// <summary>
    ///     <paramref name="member" /> as <paramref name="type" /> declares it: the member with the same
    ///     definition in the construction of its containing type that <paramref name="type" /> is, derives
    ///     from or implements, with the type arguments of the containing type but for nullable annotations.
    ///     <see langword="null" /> when there is none, as for an extension method.
    /// </summary>
    private static TMember? FindMember<TMember>(ITypeSymbol type, TMember member)
        where TMember : class, ISymbol
    {
        if (member.ContainingType is not { } owner || FindConstruction(type, owner.OriginalDefinition, owner) is not
                { } construction)
            return null;

        foreach (var candidate in construction.GetMembers(member.Name))
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, member.OriginalDefinition))
                return candidate as TMember;

        return null;
    }

    /// <summary>
    ///     True when <paramref name="member" /> is an extension member whose receiver type lacks a <c>?</c> that
    ///     <paramref name="type" /> has: <c>this Box&lt;string&gt;.Full</c> read from a <c>Box&lt;string?&gt;.Full</c>.
    ///     Roslyn binds such a member all the same, so what it supplies may be null although it says otherwise.
    /// </summary>
    private static bool ReceiverLacksAnnotation(ISymbol member, ITypeSymbol type)
    {
        return GetReceiverType(member) is { } receiver
               && FindConstruction(type, receiver.OriginalDefinition) is { } seen
               && LacksAnnotation(receiver, seen);
    }

    /// <summary>
    ///     The receiver type of an extension member: the parameter of its extension block, or the first parameter
    ///     of an extension method in its static form. <see langword="null" /> for any other member.
    /// </summary>
    private static ITypeSymbol? GetReceiverType(ISymbol member)
    {
        return member switch
        {
            { ContainingType: { IsExtension: true, ExtensionParameter: { } receiver } } => receiver.Type,
            IMethodSymbol { IsExtensionMethod: true, Parameters: { IsEmpty: false } parameters } => parameters[0].Type,
            _ => null
        };
    }

    /// <summary>
    ///     True when <paramref name="pattern" /> matches every value that <paramref name="input" /> can hold.
    ///     The declared type is needed because <see cref="IPatternOperation.InputType" /> does not tell
    ///     <c>string</c> from <c>string?</c>.
    /// </summary>
    private bool IsIrrefutable(IPatternOperation pattern, DeclaredInput input)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
            case IDeclarationPatternOperation { MatchesNull: true }: // var x
                return true;
            case IDeclarationPatternOperation { MatchedType: { } declaredType }:
                return AlwaysMatches(input, declaredType);
            case ITypePatternOperation typePattern:
                return AlwaysMatches(input, typePattern.MatchedType);
            case IRecursivePatternOperation recursive:
                return AlwaysMatches(input, recursive.MatchedType) && HasIrrefutableSubpatterns(recursive, input);
            case INegatedPatternOperation negated:
                return IsNullConstant(negated.Pattern) && CannotBeNull(input);
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either:
                return IsIrrefutable(either.LeftPattern, input) || IsIrrefutable(either.RightPattern, input);
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both:
                return IsIrrefutable(both.LeftPattern, input) && IsIrrefutable(both.RightPattern, input);
            default: // constant, relational, list and slice patterns
                return false;
        }
    }

    /// <summary>True when a test for <paramref name="testedType" /> matches every value of <paramref name="input" />.</summary>
    private bool AlwaysMatches(DeclaredInput input, ITypeSymbol testedType)
    {
        return CannotBeNull(input) && AlwaysConvertsTo(input.Type, testedType);
    }

    /// <summary>
    ///     True when every non-null value of <paramref name="source" /> is a <paramref name="destination" />:
    ///     the conversion is an identity, an implicit reference or a boxing conversion.
    /// </summary>
    private bool AlwaysConvertsTo(ITypeSymbol source, ITypeSymbol destination)
    {
        var conversion = _compilation.ClassifyConversion(source, destination);
        return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing));
    }

    /// <summary>
    ///     True unless the declared type is annotated <c>?</c> or is a <c>Nullable&lt;T&gt;</c>, or cannot be
    ///     trusted and is not a value type. A declared <c>int?</c> is annotated too; one that arrives as an
    ///     <see cref="IPatternOperation.InputType" /> is not.
    /// </summary>
    private static bool CannotBeNull(DeclaredInput input)
    {
        var type = input.Type;
        return type.NullableAnnotation != NullableAnnotation.Annotated
               && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
               && (type.IsValueType || !input.MayHideNull);
    }

    private static bool IsNullConstant(IPatternOperation pattern)
    {
        return pattern is IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } };
    }

    /// <summary>What the pattern of one arm does with one case. The patterns of several arms combine with "or".</summary>
    [Flags]
    private enum ArmMatch
    {
        None = 0,

        /// <summary>A type test names the case; the pattern may still reject some values of the case.</summary>
        Named = 1,

        /// <summary>Every non-null value of the case matches.</summary>
        Covered = 2,

        /// <summary>Every non-null value of the case matches, and that rests on a type test that names the case.</summary>
        Own = 4
    }

    /// <summary>
    ///     One case of the union of a switched value: the static type of the value, the case definition, and
    ///     what <see cref="TryBind" /> bound the type parameters of the case to.
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

    /// <summary>
    ///     The declared type of the input of a pattern. It is read from the case as the switched value
    ///     constructs it (<c>string?</c> for <c>Ok.Value</c> of a <c>Result&lt;string?&gt;</c>), member by
    ///     member. Where a member cannot be found there (an extension <c>Deconstruct</c> or an extension
    ///     property), the type is the one the arm's own construction declares.
    /// </summary>
    private readonly struct DeclaredInput(ITypeSymbol type, bool mayHideNull, bool armLacksAnnotation)
    {
        public ITypeSymbol Type { get; } = type;

        /// <summary>
        ///     True when <see cref="Type" />, or the type it was read from, is only known as the arm declares
        ///     it and that can lack a <c>?</c> of the value: a reference type may then hold null all the same.
        /// </summary>
        public bool MayHideNull { get; } = mayHideNull;

        /// <summary>The input of a subpattern whose member was found in <see cref="Type" />.</summary>
        public DeclaredInput WithMember(ITypeSymbol memberType)
        {
            return new DeclaredInput(memberType, MayHideNull, armLacksAnnotation);
        }

        /// <summary>
        ///     The input of a subpattern whose member is only known as the arm declares it. That type can
        ///     lack a <c>?</c> of the value when the arm names a type that lacks one, and when the member may
        ///     lack one (<paramref name="memberMayLackAnnotation" />): when it is generic, as Roslyn constructs
        ///     <c>Deconstruct&lt;string&gt;</c> for a <c>Box&lt;string?&gt;</c>, or when its receiver type lacks one.
        /// </summary>
        public DeclaredInput WithoutMember(ITypeSymbol memberTypeOfArm, bool memberMayLackAnnotation)
        {
            return new DeclaredInput(memberTypeOfArm, MayHideNull || armLacksAnnotation || memberMayLackAnnotation,
                armLacksAnnotation);
        }
    }
}