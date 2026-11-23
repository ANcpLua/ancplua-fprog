using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>Follows one write's receiver or storage back to the parameter or field that owns it.</summary>
internal sealed class MutationRoots(LocalAliases aliases, MutationOwner owner, ISymbol containingSymbol)
{
    private HashSet<ILocalSymbol>? _active;

    // A cycle can preserve an alias, provided an assignment in the cycle establishes a root and
    // no assignment leaves it. A root in construction is also a known source, exempt from reporting.
    public readonly struct Match(ISymbol? symbol, int start)
    {
        public ISymbol? Symbol { get; } = symbol;
        public int Start { get; } = start;
        public bool Known { get; } = true;

        public Match Include(Match alternative)
        {
            return Symbol is not null || !alternative.Known ? this : alternative;
        }
    }

    public Match Value(IOperation value)
    {
        return Trace(value, value.Syntax.SpanStart, false, [], true);
    }

    public Match Target(IOperation target, bool rebind)
    {
        // Changing only the parameter's own slot is covered by the signature rule for ref or out.
        if (target is IParameterReferenceOperation || (rebind && (owner == MutationOwner.Field || target is ILocalReferenceOperation)))
            return default;

        return Storage(target, target.Syntax.SpanStart, true);
    }

    public Match Event(IOperation target)
    {
        if (target is IEventReferenceOperation evt
            && owner == MutationOwner.Field && IsOwnMember(evt.Event, evt.Instance))
            return FieldMatch(evt.Event, target.Syntax.SpanStart);

        return Target(target, false);
    }

    private Match Storage(IOperation? target, int start, bool widen)
    {
        return target switch
        {
            ILocalReferenceOperation { Local.IsRef: true } local => ReadLocal(local.Local, start, false, [], true),
            IParameterReferenceOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } parameter
                when owner == MutationOwner.Parameter => new Match(parameter.Parameter, start),
            IConditionalOperation { IsRef: true } conditional =>
                Storage(conditional.WhenTrue, start, widen).Include(Storage(conditional.WhenFalse, start, widen)),
            IParenthesizedOperation parentheses => Storage(parentheses.Operand, start, widen),
            IInvocationOperation invocation when invocation.TargetMethod.RefKind != RefKind.None =>
                Trace(Mutators.CallReceiver(invocation), start, true, [], widen),
            _ => target is null ? default : Trace(ReceiverOfRead(target), start, ReturnsByRef(target), [], widen)
        };
    }

    private Match Trace(IOperation? value, int start, bool shared, ImmutableArray<LocalAliases.Read> reads, bool widen)
    {
        while (value is not null)
        {
            switch (value)
            {
                case ILocalReferenceOperation local:
                    return ReadLocal(local.Local, start, shared, reads, false);
                case IParenthesizedOperation parentheses:
                    value = parentheses.Operand;
                    continue;
                case IConditionalOperation conditional:
                    return Trace(conditional.WhenTrue, start, shared, reads, widen)
                        .Include(Trace(conditional.WhenFalse, start, shared, reads, widen));
                case ICoalesceOperation coalesce:
                    return Trace(coalesce.Value, start, shared, reads, widen)
                        .Include(Trace(coalesce.WhenNull, start, shared, reads, widen));
                case ISwitchExpressionOperation expression:
                    var result = default(Match);
                    foreach (var arm in expression.Arms)
                    {
                        result = result.Include(Trace(arm.Value, start, shared, reads, widen));
                        if (result.Symbol is not null)
                            break;
                    }

                    return result;
                case ITupleOperation tuple when !reads.IsEmpty && reads[0].TupleIndex >= 0
                                                && reads[0].TupleIndex < tuple.Elements.Length:
                    value = tuple.Elements[reads[0].TupleIndex];
                    reads = reads.RemoveAt(0);
                    continue;
                case IConversionOperation conversion:
                    // A reference cast or a conversion to a built-in view preserves the underlying value.
                    if (!IsPreservingConversion(conversion))
                        return default;

                    value = conversion.Operand;
                    continue;
                case IConditionalAccessOperation access:
                    value = access.WhenNotNull;
                    continue;
                case IConditionalAccessInstanceOperation instance:
                    if (EnclosingAccess(instance) is not { } enclosing)
                        return default;

                    if (widen)
                        start = Math.Min(start, enclosing.Syntax.SpanStart);
                    value = enclosing.Operation;
                    continue;
            }

            shared |= SharesStorage(value.Type) || ReturnsByRef(value);
            switch (value)
            {
                case IParameterReferenceOperation { Parameter: var parameter }:
                    return owner == MutationOwner.Parameter && IsSharedParameter(parameter)
                        ? new Match(parameter, start) : default;
                case IFieldReferenceOperation field
                    when owner == MutationOwner.Field && IsOwnMember(field.Field, field.Instance):
                    return shared || SharesProjectedValue(reads) ? FieldMatch(field.Field, start) : default;
                case IPropertyReferenceOperation property
                    when owner == MutationOwner.Field && IsOwnMember(property.Property, property.Instance)
                        && HasBackingField(property.Property):
                    return shared || SharesProjectedValue(reads) ? FieldMatch(property.Property, start) : default;
                case IFieldReferenceOperation { Instance: { } instance } field
                    when LocalAliases.GetTupleIndex(field.Field) is >= 0 and var tupleIndex:
                    reads = reads.Insert(0, new LocalAliases.Read(field.Type, tupleIndex));
                    value = instance;
                    continue;
                case IInvocationOperation invocation:
                    value = Mutators.ViewReceiver(invocation) ?? Mutators.ElementReceiver(invocation, invocation.Type);
                    continue;
                default:
                    value = ReceiverOfRead(value);
                    continue;
            }
        }

        return default;
    }

    private Match ReadLocal(ILocalSymbol local, int start, bool shared, ImmutableArray<LocalAliases.Read> reads, bool storage)
    {
        // A copied struct is independent. A span or memory is a view, and a known tuple projection
        // can select a reference that was assigned as one component of a tuple expression.
        if (!local.IsRef && (storage || !SharesStorage(local.Type) && (reads.IsEmpty || reads[0].TupleIndex < 0)))
            return default;

        var active = _active ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        if (!active.Add(local))
            return new Match(null, start);

        var result = default(Match);
        foreach (var assignment in aliases.Assignments(local))
        {
            var part = storage && assignment.Reads.IsDefaultOrEmpty
                ? Storage(assignment.Value, start, false)
                : Trace(assignment.Value, start, shared,
                    assignment.Reads.IsDefaultOrEmpty ? reads : assignment.Reads.AddRange(reads), false);
            if (!part.Known)
            {
                result = default;
                break;
            }

            result = result.Include(part);
        }

        active.Remove(local);
        return result;
    }

    private static bool IsSharedParameter(IParameterSymbol parameter)
    {
        return !Mutators.IsPrintMembersParameter(parameter)
               && (Mutators.MayBeReference(parameter.Type) || Mutators.IsMutableView(parameter.Type)
                   || parameter.RefKind is RefKind.Ref or RefKind.Out);
    }

    private static bool SharesStorage(ITypeSymbol? type)
    {
        return Mutators.MayBeReference(type) || Mutators.IsView(type);
    }

    private static bool SharesProjectedValue(ImmutableArray<LocalAliases.Read> reads)
    {
        foreach (var read in reads)
            if (SharesStorage(read.Type))
                return true;

        return false;
    }

    private static bool IsPreservingConversion(IConversionOperation conversion)
    {
        if (conversion.Conversion.IsIdentity || conversion.Conversion.IsReference)
            return true;

        return Mutators.IsView(conversion.Type)
               && (conversion.OperatorMethod is null || Mutators.IsView(conversion.OperatorMethod.ContainingType))
               && (Mutators.IsView(conversion.Operand.Type) || conversion.Operand.Type is IArrayTypeSymbol
                   || conversion.Operand.Type?.SpecialType == SpecialType.System_String
                   || Mutators.IsSystemType(conversion.Operand.Type, "ArraySegment", 1));
    }

    private static IOperation? ReceiverOfRead(IOperation operation)
    {
        return operation switch
        {
            IFieldReferenceOperation field => field.Instance,
            IEventReferenceOperation evt => evt.Instance,
            IPropertyReferenceOperation property when PreservesStorage(property) => property.Instance,
            IArrayElementReferenceOperation element when !IsArraySlice(element) => element.ArrayReference,
            IImplicitIndexerReferenceOperation indexer when !IsRange(indexer.Argument) || Mutators.IsView(indexer.Type) =>
                indexer.Instance,
            IInlineArrayAccessOperation inlineArray => inlineArray.Instance,
            IDynamicMemberReferenceOperation dynamicMember => dynamicMember.Instance,
            IDynamicIndexerAccessOperation dynamicIndexer => dynamicIndexer.Operation,
            _ => null
        };
    }

    private static bool PreservesStorage(IPropertyReferenceOperation property)
    {
        if (Mutators.IsView(property.Type))
            return true;

        foreach (var argument in property.Arguments)
            if (IsRange(argument.Value))
                return false;

        return true;
    }

    private static bool IsArraySlice(IArrayElementReferenceOperation element)
    {
        return element.Indices.Length == 1 && IsRange(element.Indices[0]);
    }

    private static bool IsRange(IOperation index) => Mutators.IsSystemType(index.Type, "Range");

    private static bool ReturnsByRef(IOperation operation)
    {
        return operation switch
        {
            IFieldReferenceOperation field => field.Field.RefKind != RefKind.None,
            IPropertyReferenceOperation property => property.Property.RefKind != RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property } => property.RefKind != RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IMethodSymbol method } => method.RefKind != RefKind.None,
            _ => false
        };
    }

    private static IConditionalAccessOperation? EnclosingAccess(IOperation instance)
    {
        var previous = instance;
        for (var current = instance.Parent; current is not null; current = current.Parent)
        {
            if (current is IConditionalAccessOperation access && access.WhenNotNull == previous)
                return access;

            previous = current;
        }

        return null;
    }

    private static bool IsOwnMember(ISymbol member, IOperation? instance)
    {
        while (instance is IConversionOperation conversion && IsPreservingConversion(conversion))
            instance = conversion.Operand;

        return member.IsStatic
               || instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };
    }

    private static bool HasBackingField(IPropertySymbol property)
    {
        if (property.IsIndexer || !CoreScope.IsDeclaredInSource(property))
            return false;

        foreach (var member in property.ContainingType.GetMembers("<" + property.Name + ">k__BackingField"))
            if (member is IFieldSymbol field && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property))
                return true;

        return false;
    }

    private Match FieldMatch(ISymbol field, int start)
    {
        return new Match(IsConstruction(field) ? null : field, start);
    }

    private bool IsConstruction(ISymbol field)
    {
        var scope = containingSymbol;
        while (scope is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
            scope = scope.ContainingSymbol;

        var building = scope switch
        {
            IMethodSymbol { MethodKind: MethodKind.StaticConstructor } => field.IsStatic,
            IMethodSymbol { MethodKind: MethodKind.Constructor } => !field.IsStatic,
            IFieldSymbol or IPropertySymbol or IEventSymbol => scope.IsStatic == field.IsStatic,
            _ => false
        };
        if (!building)
            return false;

        if (field.IsStatic)
            return SymbolEqualityComparer.Default.Equals(scope.ContainingType, field.ContainingType);

        for (var type = scope.ContainingType; type is not null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, field.ContainingType.OriginalDefinition))
                return true;

        return false;
    }
}
