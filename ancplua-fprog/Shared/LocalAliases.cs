using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>
///     The sources assigned to each local in an operation block. A source can project a component of a tuple,
///     a property or positional subpattern, or an element from iteration. The table is built on first use and
///     shared read-only by all of the block's operation actions.
/// </summary>
internal sealed class LocalAliases
{
    private readonly Lazy<Dictionary<ILocalSymbol, LocalAssignments>> _sources;

    public LocalAliases(ImmutableArray<IOperation> blocks, CancellationToken cancellationToken)
    {
        _sources = new Lazy<Dictionary<ILocalSymbol, LocalAssignments>>(() => Build(blocks, cancellationToken));
    }

    private sealed class LocalAssignments
    {
        public List<Source> Whole { get; } = [];
        public List<Assignment>? Slots { get; set; }
    }

    public IReadOnlyList<Source> Assignments(ILocalSymbol local)
    {
        return _sources.Value.TryGetValue(local, out var assignments) ? assignments.Whole : Array.Empty<Source>();
    }

    public IEnumerable<Assignment> AssignmentsIncludingSlots(ILocalSymbol local)
    {
        if (!_sources.Value.TryGetValue(local, out var assignments))
            yield break;

        foreach (var source in assignments.Whole)
            yield return new Assignment(source, []);
        if (assignments.Slots is { } slots)
            foreach (var assignment in slots)
                yield return assignment;
    }

    /// <summary>An assignment to the entire local or to the tuple slot selected by a sequence of ordinals.</summary>
    public readonly struct Assignment(Source source, ImmutableArray<int> slots)
    {
        public Source Source { get; } = source;
        public ImmutableArray<int> Slots { get; } = slots;

        public bool TrySelect(ImmutableArray<Read> reads, out ImmutableArray<Read> remaining)
        {
            remaining = reads;
            if (Slots.IsDefaultOrEmpty)
                return true;

            if (Slots.Length > reads.Length)
                return false;

            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i] != reads[i].TupleIndex)
                    return false;

            remaining = reads.RemoveRange(0, Slots.Length);
            return true;
        }
    }

    /// <summary>The expression assigned, followed by any unwritten reads that select the assigned value.</summary>
    public readonly struct Source(IOperation? value)
    {
        private Source(IOperation value, ImmutableArray<Read> reads) : this(value)
        {
            Reads = reads;
        }

        public IOperation? Value { get; } = value;

        public ImmutableArray<Read> Reads { get; } = [];

        public ITypeSymbol? Type => Reads.IsDefaultOrEmpty ? Value?.Type : Reads[Reads.Length - 1].Type;

        // A source binding that boxes a value gives the target an independent copy of its storage.
        public Source StoredAs(ITypeSymbol? targetType)
        {
            return Type is { IsValueType: true } && Mutators.MayBeReference(targetType) ? default : this;
        }

        public Source Select(ITypeSymbol? type, int tupleIndex = -1)
        {
            return Value is { } value ? new Source(value, Reads.Add(new Read(type, tupleIndex))) : default;
        }
    }

    /// <summary>A projected read. A tuple index lets the search select just that branch of a tuple expression.</summary>
    public readonly struct Read(ITypeSymbol? type, int tupleIndex)
    {
        public ITypeSymbol? Type { get; } = type;
        public int TupleIndex { get; } = tupleIndex;
    }

    /// <summary>Returns the ordinal of a tuple field, including a field addressed by its declared name.</summary>
    public static int GetTupleIndex(IFieldSymbol field)
    {
        if (field.CorrespondingTupleField is not { } ordinal)
            return -1;

        // Roslyn can expose differently constructed field symbols for an inferred tuple's named and
        // ordinal access. Each default field name identifies one slot in this same tuple type.
        var fields = field.ContainingType.TupleElements;
        for (var index = 0; index < fields.Length; index++)
            if (fields[index].CorrespondingTupleField?.Name == ordinal.Name)
                return index;

        return -1;
    }

    /// <summary>Returns the variables in a deconstruction target through any declaration wrappers.</summary>
    public static IOperation UnwrapDeclaration(IOperation operation)
    {
        return operation is IDeclarationExpressionOperation declaration ? declaration.Expression : operation;
    }

    private static Dictionary<ILocalSymbol, LocalAssignments> Build(
        ImmutableArray<IOperation> blocks, CancellationToken cancellationToken)
    {
        var builder = new Builder();
        foreach (var block in blocks)
        foreach (var operation in block.DescendantsAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Add(operation, cancellationToken);
        }

        return builder.Finish(cancellationToken);
    }

    private sealed class Builder
    {
        private readonly Dictionary<ILocalSymbol, LocalAssignments> _assignments = new(SymbolEqualityComparer.Default);
        private readonly List<(ILocalSymbol Alias, Assignment Write)> _refWrites = [];

        public void Add(IOperation operation, CancellationToken cancellationToken)
        {
            switch (operation)
            {
                case IVariableDeclaratorOperation declarator when declarator.GetVariableInitializer() is { Value: var value }:
                    AddAssignment(declarator.Symbol, new Source(value));
                    break;
                case IDeconstructionAssignmentOperation deconstruction:
                    var info = deconstruction is { Syntax: AssignmentExpressionSyntax syntax, SemanticModel: { } model }
                        ? model.GetDeconstructionInfo(syntax)
                        : default;
                    Deconstruct(deconstruction.Target, new Source(deconstruction.Value), info);
                    break;
                case ISimpleAssignmentOperation { IsRef: true } assignment:
                    if (assignment.Target is ILocalReferenceOperation rebind)
                        AddAssignment(rebind.Local, new Source(assignment.Value));
                    break;
                case ISimpleAssignmentOperation assignment:
                    Write(assignment.Target, new Source(assignment.Value));
                    break;
                case ICoalesceAssignmentOperation assignment:
                    Write(assignment.Target, new Source(assignment.Value));
                    break;
                case IAssignmentOperation assignment:
                    Write(assignment.Target, default);
                    break;
                case IIncrementOrDecrementOperation increment:
                    Write(increment.Target, default);
                    break;
                case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                    Write(argument.Value, OutElement(argument));
                    break;
                case IAddressOfOperation address:
                    Write(address.Reference, default);
                    break;
                case IForEachLoopOperation loop:
                    AddIteration(loop);
                    break;
                case IIsPatternOperation test:
                    AddPattern(test.Pattern, new Source(test.Value), cancellationToken);
                    break;
                case IPatternCaseClauseOperation { Parent.Parent: ISwitchOperation statement } label:
                    AddPattern(label.Pattern, new Source(statement.Value), cancellationToken);
                    break;
                case ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation expression } arm:
                    AddPattern(arm.Pattern, new Source(expression.Value), cancellationToken);
                    break;
            }
        }

        public Dictionary<ILocalSymbol, LocalAssignments> Finish(CancellationToken cancellationToken)
        {
            // Writing through a ref alias to a variable also changes what that variable stands for.
            foreach (var (alias, write) in _refWrites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var visited = new Dictionary<ILocalSymbol, List<ImmutableArray<int>>>(SymbolEqualityComparer.Default);
                WriteThrough(alias, write, visited);
            }

            return _assignments;
        }

        private void WriteThrough(ILocalSymbol alias, Assignment write,
            Dictionary<ILocalSymbol, List<ImmutableArray<int>>> visited)
        {
            if (!_assignments.TryGetValue(alias, out var bindings))
                return;

            if (!visited.TryGetValue(alias, out var paths))
                visited.Add(alias, paths = []);
            else if (paths.Any(path => path.SequenceEqual(write.Slots)))
                return;
            paths.Add(write.Slots);

            foreach (var sourceBinding in bindings.Whole)
                if (sourceBinding.Reads.IsDefaultOrEmpty)
                    WriteStorage(sourceBinding.Value, write, visited);
        }

        private void WriteStorage(IOperation? target, Assignment write,
            Dictionary<ILocalSymbol, List<ImmutableArray<int>>>? visited)
        {
            switch (target)
            {
                case ILocalReferenceOperation { Local.IsRef: true } reference:
                    if (visited is null)
                        _refWrites.Add((reference.Local, write));
                    else
                        WriteThrough(reference.Local, write, visited);
                    break;
                case ILocalReferenceOperation reference:
                    AddAssignment(reference.Local, write);
                    break;
                case IFieldReferenceOperation field when GetTupleIndex(field.Field) is >= 0 and var index:
                    WriteStorage(field.Instance, new Assignment(write.Source, write.Slots.Insert(0, index)), visited);
                    break;
                case IConditionalOperation { IsRef: true } conditional:
                    WriteStorage(conditional.WhenTrue, write, visited);
                    WriteStorage(conditional.WhenFalse, write, visited);
                    break;
                case IParenthesizedOperation parenthesized:
                    WriteStorage(parenthesized.Operand, write, visited);
                    break;
            }
        }

        private void Deconstruct(IOperation target, Source source, DeconstructionInfo info)
        {
            if (UnwrapDeclaration(target) is not ITupleOperation tuple)
            {
                // A user-defined conversion into the target hands over a value of its own.
                Write(target, info.Conversion is { IsUserDefined: true } ? default : source);
                return;
            }

            for (var index = 0; index < tuple.Elements.Length; index++)
            {
                var targetElement = tuple.Elements[index];
                var tupleIndex = source.Type is { IsTupleType: true } ? index : -1;
                var selectedType = source.Type is INamedTypeSymbol { IsTupleType: true } sourceTuple
                                   && index < sourceTuple.TupleElements.Length
                    ? sourceTuple.TupleElements[index].Type
                    : DeconstructedType(info.Method, index, targetElement.Type);
                var nested = !info.Nested.IsDefaultOrEmpty && index < info.Nested.Length
                    ? info.Nested[index]
                    : default;
                Deconstruct(targetElement, source.Select(selectedType, tupleIndex), nested);
            }
        }

        private static ITypeSymbol? DeconstructedType(IMethodSymbol? method, int index, ITypeSymbol? fallback)
        {
            if (method is null)
                return fallback;

            foreach (var parameter in method.Parameters)
                if (parameter.RefKind == RefKind.Out && index-- == 0)
                    return parameter.Type;

            return fallback;
        }

        private void AddIteration(IForEachLoopOperation loop)
        {
            if (loop is not { Syntax: CommonForEachStatementSyntax syntax, SemanticModel: { } model })
                return;

            var iteration = model.GetForEachStatementInfo(syntax);
            var element = new Source(loop.Collection).Select(iteration.ElementType);
            if (loop.LoopControlVariable is IVariableDeclaratorOperation variable)
                // A user-defined conversion into the loop variable hands over a value of its own.
                AddAssignment(variable.Symbol, iteration.ElementConversion.IsUserDefined ? default : element);
            else if (syntax is ForEachVariableStatementSyntax deconstruction)
                Deconstruct(loop.LoopControlVariable, element, model.GetDeconstructionInfo(deconstruction));
        }

        private void AddPattern(IPatternOperation pattern, Source source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (pattern)
            {
                case IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol local }:
                    AddAssignment(local, source);
                    break;
                case IBinaryPatternOperation binary:
                    AddPattern(binary.LeftPattern, source, cancellationToken);
                    AddPattern(binary.RightPattern, source, cancellationToken);
                    break;
                case INegatedPatternOperation negated:
                    AddPattern(negated.Pattern, source, cancellationToken);
                    break;
                case IRecursivePatternOperation recursive:
                    if (recursive.DeclaredSymbol is ILocalSymbol designation)
                        AddAssignment(designation, source);

                    for (var index = 0; index < recursive.DeconstructionSubpatterns.Length; index++)
                    {
                        var subpattern = recursive.DeconstructionSubpatterns[index];
                        var tupleIndex = recursive.MatchedType is { IsTupleType: true } ? index : -1;
                        AddPattern(subpattern, source.Select(subpattern.InputType, tupleIndex), cancellationToken);
                    }

                    foreach (var property in recursive.PropertySubpatterns)
                    {
                        var tupleIndex = property.Member is IFieldReferenceOperation field ? GetTupleIndex(field.Field) : -1;
                        AddPattern(property.Pattern, source.Select(property.Member.Type, tupleIndex), cancellationToken);
                    }

                    break;
                case IListPatternOperation list:
                    if (list.DeclaredSymbol is ILocalSymbol entire)
                        AddAssignment(entire, source);

                    foreach (var element in list.Patterns)
                        AddPattern(element, source.Select(element.InputType), cancellationToken);

                    break;
                case ISlicePatternOperation { Pattern: { } slice }:
                    // An array slice allocates a new array. Span and memory slices keep their storage.
                    AddPattern(slice, Mutators.IsView(slice.InputType) ? source : default, cancellationToken);
                    break;
            }
        }

        private static Source OutElement(IArgumentOperation argument)
        {
            if (argument.Parameter is not { RefKind: RefKind.Out, Type: var type }
                || argument.Parent is not IInvocationOperation invocation
                || Mutators.ElementReceiver(invocation, type) is not { } receiver)
                return default;

            return new Source(receiver).Select(type);
        }

        private void Write(IOperation target, Source source)
        {
            target = UnwrapDeclaration(target);
            WriteStorage(target, new Assignment(source.StoredAs(target.Type), []), null);
        }

        private void AddAssignment(ILocalSymbol local, Source source)
        {
            AddAssignment(local, new Assignment(source.StoredAs(local.Type), []));
        }

        private void AddAssignment(ILocalSymbol local, Assignment assignment)
        {
            if (!_assignments.TryGetValue(local, out var assignments))
            {
                assignments = new LocalAssignments();
                _assignments.Add(local, assignments);
            }

            if (assignment.Slots.IsEmpty)
                assignments.Whole.Add(assignment.Source);
            else
                (assignments.Slots ??= []).Add(assignment);
        }
    }
}
