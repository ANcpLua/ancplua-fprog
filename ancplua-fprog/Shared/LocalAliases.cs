using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    private readonly Lazy<Dictionary<ILocalSymbol, List<Source>>> _sources;

    public LocalAliases(ImmutableArray<IOperation> blocks, CancellationToken cancellationToken)
    {
        _sources = new Lazy<Dictionary<ILocalSymbol, List<Source>>>(() => Build(blocks, cancellationToken));
    }

    public IReadOnlyList<Source> Assignments(ILocalSymbol local)
    {
        return _sources.Value.TryGetValue(local, out var assignments) ? assignments : Array.Empty<Source>();
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
        if (!field.ContainingType.IsTupleType)
            return -1;

        var fields = field.ContainingType.TupleElements;
        for (var index = 0; index < fields.Length; index++)
            if (SymbolEqualityComparer.Default.Equals(fields[index].CorrespondingTupleField, field.CorrespondingTupleField))
                return index;

        return -1;
    }

    /// <summary>Returns the variables in a deconstruction target through any declaration wrappers.</summary>
    public static IOperation UnwrapDeclaration(IOperation operation)
    {
        return operation is IDeclarationExpressionOperation declaration ? declaration.Expression : operation;
    }

    private static Dictionary<ILocalSymbol, List<Source>> Build(
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
        private readonly Dictionary<ILocalSymbol, List<Source>> _assignments = new(SymbolEqualityComparer.Default);
        private readonly List<(ILocalSymbol Alias, Source Source)> _refWrites = [];

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
                case ISimpleAssignmentOperation assignment when assignment.IsRef:
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

        public Dictionary<ILocalSymbol, List<Source>> Finish(CancellationToken cancellationToken)
        {
            // Writing through a ref alias to a variable also changes what that variable stands for.
            foreach (var (alias, source) in _refWrites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var visited = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
                WriteThrough(alias, source, visited);
            }

            return _assignments;
        }

        private void WriteThrough(ILocalSymbol alias, Source source, HashSet<ILocalSymbol> visited)
        {
            if (!visited.Add(alias) || !_assignments.TryGetValue(alias, out var assignments))
                return;

            foreach (var assignment in assignments)
                if (assignment.Reads.IsDefaultOrEmpty)
                    WriteStorage(assignment.Value, source, visited);
        }

        private void WriteStorage(IOperation? target, Source source, HashSet<ILocalSymbol> visited)
        {
            switch (target)
            {
                case ILocalReferenceOperation { Local.IsRef: true } reference:
                    WriteThrough(reference.Local, source, visited);
                    break;
                case ILocalReferenceOperation reference:
                    AddAssignment(reference.Local, source);
                    break;
                case IConditionalOperation { IsRef: true } conditional:
                    WriteStorage(conditional.WhenTrue, source, visited);
                    WriteStorage(conditional.WhenFalse, source, visited);
                    break;
                case IParenthesizedOperation parenthesized:
                    WriteStorage(parenthesized.Operand, source, visited);
                    break;
            }
        }

        private void Deconstruct(IOperation target, Source source, DeconstructionInfo info)
        {
            if (UnwrapDeclaration(target) is not ITupleOperation tuple)
            {
                Write(target, source);
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

            var element = new Source(loop.Collection).Select(model.GetForEachStatementInfo(syntax).ElementType);
            if (loop.LoopControlVariable is IVariableDeclaratorOperation variable)
                AddAssignment(variable.Symbol, element);
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
            switch (UnwrapDeclaration(target))
            {
                case ILocalReferenceOperation { Local.IsRef: true } reference:
                    _refWrites.Add((reference.Local, source));
                    break;
                case ILocalReferenceOperation reference:
                    AddAssignment(reference.Local, source);
                    break;
                case IConditionalOperation { IsRef: true } conditional:
                    if (conditional.WhenTrue is { } whenTrue)
                        Write(whenTrue, source);
                    if (conditional.WhenFalse is { } whenFalse)
                        Write(whenFalse, source);
                    break;
            }
        }

        private void AddAssignment(ILocalSymbol local, Source source)
        {
            if (!_assignments.TryGetValue(local, out var assignments))
            {
                assignments = [];
                _assignments.Add(local, assignments);
            }

            // A binding that boxes a value gets an independent copy of its storage.
            assignments.Add(source.Type is { IsValueType: true } && Mutators.MayBeReference(local.Type)
                ? default
                : source);
        }
    }
}
