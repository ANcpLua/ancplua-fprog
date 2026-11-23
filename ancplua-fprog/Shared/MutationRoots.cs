using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>Resolves writes and the local aliases they reach within one operation block.</summary>
internal sealed class MutationRoots(
    LocalAliases aliases, MutationOwner owner, ISymbol containingSymbol, CancellationToken cancellationToken)
{
    private readonly object _gate = new();
    private Dictionary<LocalRead, Match>? _resolved;
    private Dictionary<ILocalSymbol, bool>? _acyclicLocals;
    private Dictionary<IOperation, bool>? _acyclicValues;
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
        lock (_gate)
            return Trace(value, value.Syntax.SpanStart, false, [], true);
    }

    public Match Target(IOperation target, bool rebind)
    {
        // Changing only the parameter's own slot is covered by the signature rule for ref or out.
        if (target is IParameterReferenceOperation || (rebind && (owner == MutationOwner.Field || target is ILocalReferenceOperation)))
            return default;

        lock (_gate)
            return Storage(target, target.Syntax.SpanStart, true, rebind);
    }

    public Match Event(IOperation target)
    {
        if (target is IEventReferenceOperation evt
            && owner == MutationOwner.Field && IsOwnMember(evt.Event, evt.Instance))
            return FieldMatch(evt.Event, target.Syntax.SpanStart);

        return Target(target, false);
    }

    private Match Storage(IOperation? target, int start, bool widen, bool rebind = false)
    {
        return target switch
        {
            ILocalReferenceOperation { Local.IsRef: true } local => ReadLocal(local.Local, start, false, [], true),
            IParameterReferenceOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } parameter
                when owner == MutationOwner.Parameter => new Match(parameter.Parameter, start),
            IConditionalOperation { IsRef: true } conditional =>
                Storage(conditional.WhenTrue, start, widen, rebind)
                    .Include(Storage(conditional.WhenFalse, start, widen, rebind)),
            IParenthesizedOperation parentheses => Storage(parentheses.Operand, start, widen, rebind),
            IInvocationOperation invocation when invocation.TargetMethod.RefKind != RefKind.None =>
                Trace(Mutators.CallReceiver(invocation), start, true, [], widen),
            // Replacing the target's slot does not follow the reference that was in that slot.
            _ => target is null ? default : Trace(ReceiverOfRead(target), start, !rebind && ReturnsByRef(target), [], widen)
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
                case IParenthesizedOperation or IConversionOperation or IConditionalAccessOperation:
                    value = ValueSource(value);
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
                    return owner == MutationOwner.Parameter
                           && !Mutators.IsPrintMembersParameter(parameter)
                           && (shared || SharesProjectedValue(reads) || parameter.RefKind is RefKind.Ref or RefKind.Out)
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
                default:
                    value = ValueSource(value);
                    continue;
            }
        }

        return default;
    }

    private Match ReadLocal(ILocalSymbol local, int start, bool shared, ImmutableArray<LocalAliases.Read> reads, bool storage)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A by-value copy has its own slots. A reference or view carried by the copy can still reach the source.
        if (!local.IsRef && !shared && !SharesStorage(local.Type) && !SharesProjectedValue(reads))
            return default;

        if (!IsAcyclic(local))
            return ReadCyclicLocal(local, start, shared, reads, storage);

        var read = new LocalRead(local, shared, reads, storage);
        var resolved = _resolved ??= [];
        if (!resolved.TryGetValue(read, out var match))
        {
            // An assignment cannot widen the write's span. Only its origin is shared between writes.
            match = TraceAssignments(local, 0, shared, reads, storage);
            resolved.Add(read, match);
        }

        return match.Known ? new Match(match.Symbol, start) : default;
    }

    private Match ReadCyclicLocal(
        ILocalSymbol local, int start, bool shared, ImmutableArray<LocalAliases.Read> reads, bool storage)
    {
        var active = _active ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        if (!active.Add(local))
            return new Match(null, start);

        try
        {
            return TraceAssignments(local, start, shared, reads, storage);
        }
        finally
        {
            active.Remove(local);
        }
    }

    private Match TraceAssignments(
        ILocalSymbol local, int start, bool shared, ImmutableArray<LocalAliases.Read> reads, bool storage)
    {
        var result = default(Match);
        foreach (var assignment in aliases.Assignments(local))
        {
            var part = storage && assignment.Reads.IsDefaultOrEmpty
                ? Storage(assignment.Value, start, false)
                : Trace(assignment.Value, start, shared,
                    assignment.Reads.IsDefaultOrEmpty ? reads : assignment.Reads.AddRange(reads), false);
            if (!part.Known)
                return default;

            result = result.Include(part);
        }

        return result;
    }

    // Classify the dependency graph before resolving a read. The same local can be reached with a
    // different projection while it is active, so independence has to hold for all its reads.
    private bool IsAcyclic(ILocalSymbol local)
    {
        var known = _acyclicLocals ??= new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);
        if (known.TryGetValue(local, out var acyclic))
            return acyclic;

        known.Add(local, false);
        foreach (var assignment in aliases.Assignments(local))
            if (!IsAcyclic(assignment.Value))
                return false;

        known[local] = true;
        return true;
    }

    private bool IsAcyclic(IOperation? value)
    {
        if (value is null)
            return true;

        cancellationToken.ThrowIfCancellationRequested();
        var known = _acyclicValues ??= [];
        if (known.TryGetValue(value, out var acyclic))
            return acyclic;

        known.Add(value, false);
        acyclic = value switch
        {
            ILocalReferenceOperation local => IsAcyclic(local.Local),
            IConditionalOperation conditional => IsAcyclic(conditional.WhenTrue) && IsAcyclic(conditional.WhenFalse),
            ICoalesceOperation coalesce => IsAcyclic(coalesce.Value) && IsAcyclic(coalesce.WhenNull),
            ISwitchExpressionOperation expression => expression.Arms.All(arm => IsAcyclic(arm.Value)),
            ITupleOperation tuple => tuple.Elements.All(IsAcyclic),
            IInvocationOperation invocation when invocation.TargetMethod.RefKind != RefKind.None =>
                IsAcyclic(Mutators.CallReceiver(invocation)) && IsAcyclic(ValueSource(invocation)),
            _ => IsAcyclic(ValueSource(value))
        };
        known[value] = acyclic;
        return acyclic;
    }

    private readonly struct LocalRead(
        ILocalSymbol local, bool shared, ImmutableArray<LocalAliases.Read> reads, bool storage) : IEquatable<LocalRead>
    {
        private readonly ILocalSymbol _local = local;
        private readonly bool _shared = shared;
        private readonly ImmutableArray<LocalAliases.Read> _reads = reads;
        private readonly bool _storage = storage;

        public bool Equals(LocalRead other)
        {
            if (_shared != other._shared || _storage != other._storage || _reads.Length != other._reads.Length
                || !SymbolEqualityComparer.Default.Equals(_local, other._local))
                return false;

            for (var index = 0; index < _reads.Length; index++)
                if (_reads[index].TupleIndex != other._reads[index].TupleIndex
                    || !SymbolEqualityComparer.Default.Equals(_reads[index].Type, other._reads[index].Type))
                    return false;

            return true;
        }

        public override bool Equals(object? obj) => obj is LocalRead other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SymbolEqualityComparer.Default.GetHashCode(_local);
                hash = hash * 31 + _shared.GetHashCode();
                hash = hash * 31 + _storage.GetHashCode();
                foreach (var read in _reads)
                {
                    hash = hash * 31 + read.TupleIndex;
                    hash = hash * 31 + (read.Type is { } type ? SymbolEqualityComparer.Default.GetHashCode(type) : 0);
                }

                return hash;
            }
        }
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

    private static IOperation? ValueSource(IOperation value)
    {
        return value switch
        {
            IParenthesizedOperation parentheses => parentheses.Operand,
            IConversionOperation conversion when IsPreservingConversion(conversion) => conversion.Operand,
            IConditionalAccessOperation access => access.WhenNotNull,
            IConditionalAccessInstanceOperation instance => EnclosingAccess(instance)?.Operation,
            IInvocationOperation invocation => Mutators.ViewReceiver(invocation)
                                               ?? Mutators.ElementReceiver(invocation, invocation.Type),
            _ => ReceiverOfRead(value)
        };
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
