using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0104: inside the functional core, an assignment, compound assignment, <c>??=</c>, deconstruction
///     assignment, event subscription, <c>++</c>, <c>--</c>, or argument bound to a <c>ref</c> or <c>out</c>
///     parameter, whose target is a field, property, event or element reached from a parameter whose type may
///     be a reference (class, interface, array, delegate, <c>dynamic</c>, type parameter not known to be a value
///     type) or from a <c>ref</c>/<c>out</c> parameter.
///     <para>
///         The chain from the parameter to the target may pass through fields, properties, indexers, array elements,
///         casts, null-conditional access and method calls. When the chain meets a choice between objects
///         (<c>?:</c>, also by <c>ref</c>, <c>??</c> or a switch expression), every branch is followed, and the write
///         is reported when any branch is reached from such a parameter; a branch that is a fresh object or a boxed
///         copy of a struct does not count.
///     </para>
///     <para>
///         The chain may also pass through a local that is another name for the parameter's data, because writing
///         through it changes the caller's object just the same. Such a local has a type that may be a reference, is
///         never written again in the member (no assignment of any kind, <c>++</c>/<c>--</c>, deconstruction,
///         <c>ref</c>/<c>out</c>/<c>in</c> argument or <c>ref</c> alias), and is one of: a local declared with an
///         initializer (it stands for the initializer), a <c>foreach</c> variable (an element of the collection),
///         or a variable declared by the top-level pattern, also through <c>and</c>, of an <c>is</c>, a <c>case</c>
///         label or a switch-expression arm (the tested value). What it stands for must be an access path: a
///         parameter, another such local, or a field, property, indexer or element read from one, through casts,
///         <c>as</c>, <c>!</c> and <c>?.</c>. A method call, a range, <c>new</c>, <c>with</c>, a collection
///         expression, a choice (<c>?:</c>, <c>??</c>, switch), a dynamic access or a user-defined conversion ends
///         the path, because the result may be a fresh object (<c>var copy = values.ToArray()</c>). A local of a
///         value type is a copy, and a reference-typed pattern or <c>foreach</c> variable over a struct value holds
///         a boxed copy; neither is followed.
///     </para>
///     <para>
///         Not reported: writes through a struct parameter passed by value, <c>in</c> or <c>ref readonly</c>
///         (<c>Span&lt;T&gt;</c> and structs holding references included); writes into a boxed copy unless the
///         chain above the box reads a reference or passes through a member that returns by reference; arguments
///         to <c>in</c> and <c>ref readonly</c> parameters (<c>Volatile.Read(ref p.Total)</c>), decided by the kind
///         of the parameter and not by the keyword at the call; passing a local, an <c>out var</c> or a parameter
///         itself; assigning a ref choice of parameter variables (<c>(f ? ref a : ref b) = x</c>). Known gaps,
///         left out because the analysis cannot tell them from copies cheaply: arguments of dynamic calls, a ref
///         choice that is itself the target (<c>(f ? ref a.X : ref b.X) = 1</c>), <c>ref</c> locals and
///         <c>ref</c> iteration variables, spans over a parameter's array, locals assigned after their declaration,
///         deconstructed locals, <c>out var</c> results, nested pattern variables, and locals that stand for a
///         method call or a choice.
///     </para>
///     <para>
///         Reported at the whole assignment target expression (for an argument: the expression after
///         <c>ref</c>/<c>out</c>), including enclosing parentheses.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0104ParameterWriteAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Bounds a chain of aliases; it is finite in code that compiles.</summary>
    private const int MaxAliasDepth = 32;

    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0104",
        "Do not write through a parameter in the functional core",
        "Return a new value instead of mutating the object passed in");

    private static readonly ImmutableArray<OperationKind> Kinds =
    [
        OperationKind.SimpleAssignment,
        OperationKind.CompoundAssignment,
        OperationKind.CoalesceAssignment,
        OperationKind.DeconstructionAssignment,
        OperationKind.EventAssignment,
        OperationKind.Increment,
        OperationKind.Decrement,
        OperationKind.Argument
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockStartAction(start =>
        {
            if (!CoreScope.IsInCore(start.OwningSymbol))
                return;

            // One alias table per block: it is built at most once, on the first write whose receiver is a
            // local, so that no write rescans the member.
            var aliases = new LocalAliases(start.OperationBlocks, start.CancellationToken);
            start.RegisterOperationAction(
                operationContext => Analyze(operationContext, aliases),
                Kinds);
        });
    }

    private static void Analyze(OperationAnalysisContext context, LocalAliases aliases)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        switch (context.Operation)
        {
            case IDeconstructionAssignmentOperation deconstruction:
                Check(context, aliases, deconstruction.Target, false);
                break;
            case ISimpleAssignmentOperation simple:
                // `x.F = ref y` rebinds the ref field F; it writes nothing through the storage F referred to.
                Check(context, aliases, simple.Target, simple.IsRef);
                break;
            case IAssignmentOperation assignment:
                Check(context, aliases, assignment.Target, false);
                break;
            case IEventAssignmentOperation eventAssignment:
                Check(context, aliases, eventAssignment.EventReference, false);
                break;
            case IIncrementOrDecrementOperation increment:
                Check(context, aliases, increment.Target, false);
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                // The callee may write the storage it receives. The parameter decides, not the keyword at the
                // call: `ref x` passed to a `ref readonly` parameter, and every `in` argument, cannot be written.
                Check(context, aliases, argument.Value, false);
                break;
        }
    }

    private static void Check(OperationAnalysisContext context, LocalAliases aliases, IOperation target, bool rebinds)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
                Check(context, aliases, element, false);

            return;
        }

        var start = target.Syntax.SpanStart;
        if (Reaches(aliases, Receiver(target), !rebinds && ReturnsByRef(target), false, ref start, 0))
            Report(context, target, start);
    }

    /// <summary>
    ///     True when the receiver chain starting at <paramref name="receiver" /> leads back to a parameter whose
    ///     object or storage the caller sees. <paramref name="shared" /> is true when the chain between the
    ///     target and <paramref name="receiver" /> already read a reference or a member that returns by
    ///     reference, so a box further down no longer isolates the write. <paramref name="start" /> moves to
    ///     the start of a null-conditional access that the target's own chain passes through.
    ///     <paramref name="path" /> is true once the walk has gone through a local alias: from there on only an
    ///     access path counts (see <see cref="Fp0104ParameterWriteAnalyzer" />).
    /// </summary>
    private static bool Reaches(
        LocalAliases aliases,
        IOperation? receiver,
        bool shared,
        bool path,
        ref int start,
        int depth)
    {
        while (receiver is not null)
        {
            switch (receiver)
            {
                // `(f ? a : b).X = 1`, `(a ?? b).X = 1` and `(i switch { ... }).X = 1` write into whichever
                // object is chosen, so each branch is followed on its own. The choice itself is no step of the
                // chain: its type says nothing about whether a branch is a boxed copy. Stored in a local, a
                // choice is no access path.
                case IConditionalOperation conditional:
                    return !path
                           && (Reaches(aliases, conditional.WhenTrue, shared, path, ref start, depth)
                               || Reaches(aliases, conditional.WhenFalse, shared, path, ref start, depth));
                case ICoalesceOperation coalesce:
                    return !path
                           && (Reaches(aliases, coalesce.Value, shared, path, ref start, depth)
                               || Reaches(aliases, coalesce.WhenNull, shared, path, ref start, depth));
                case ISwitchExpressionOperation switchExpression:
                    if (path)
                        return false;

                    foreach (var arm in switchExpression.Arms)
                        if (Reaches(aliases, arm.Value, shared, path, ref start, depth))
                            return true;

                    return false;
                // Another name for an expression: the walk goes on in what the local stands for. Like a cast,
                // the local is no step of its own; whether it holds a box is decided where the value was taken.
                case ILocalReferenceOperation { Local: var local }:
                    if (depth >= MaxAliasDepth || !aliases.TryGet(local, out var alias))
                        return false;

                    if (alias.Boxes && !shared)
                        return false;

                    if (alias.ElementType is { } elementType)
                        shared |= MayBeReference(elementType);

                    path = true;
                    depth++;
                    receiver = alias.Source;
                    continue;
                case IConditionalAccessOperation access when path:
                    // `var inner = box?.Inner`: the value read when the receiver is not null.
                    receiver = access.WhenNotNull;
                    continue;
            }

            if (path && !IsAccessPathStep(receiver))
                return false;

            if (receiver is IConversionOperation boxing && IsBoxing(boxing))
            {
                // The box is a fresh copy: a write into it reaches the caller only through a reference
                // read further up the chain.
                if (!shared)
                    return false;

                receiver = boxing.Operand;
                continue;
            }

            shared |= ReturnsByRef(receiver) || MayBeReference(receiver.Type);
            switch (receiver)
            {
                case IParameterReferenceOperation { Parameter: var parameter }:
                    // A struct parameter (Span<T> included) is not a reference-type parameter unless ref/out.
                    return MayBeReference(parameter.Type) || parameter.RefKind is RefKind.Ref or RefKind.Out;
                case IConditionalAccessInstanceOperation instance:
                    var enclosing = EnclosingAccess(instance);
                    if (enclosing is null)
                        return false;
                    // Only the target's own `?.` widens the report; one inside an alias's source is elsewhere.
                    if (!path)
                        start = enclosing.Syntax.SpanStart;
                    receiver = enclosing.Operation;
                    break;
                case IConversionOperation conversion:
                    receiver = conversion.Operand;
                    break;
                default:
                    receiver = Receiver(receiver);
                    break;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when <paramref name="operation" /> may be a step of an access path, the only thing a local alias
    ///     may stand for: a parameter, another local, a field, property, indexer or array element read, a cast
    ///     or <c>as</c> without a user-defined operator, or the receiver of a <c>?.</c>. A method call ends the
    ///     path, because its result may be a fresh object (<c>values.ToArray()</c>), and so does a range
    ///     (<c>values[1..]</c> copies the array) and a dynamic access, whose meaning is decided at run time.
    /// </summary>
    private static bool IsAccessPathStep(IOperation operation)
    {
        return operation switch
        {
            IParameterReferenceOperation or IConditionalAccessInstanceOperation => true,
            IFieldReferenceOperation or IEventReferenceOperation => true,
            IPropertyReferenceOperation property => !HasRange(property.Arguments.Select(argument => argument.Value)),
            IArrayElementReferenceOperation element => !HasRange(element.Indices),
            IImplicitIndexerReferenceOperation indexer => !IsRange(indexer.Argument),
            IConversionOperation conversion => conversion.OperatorMethod is null,
            _ => false
        };
    }

    private static bool HasRange(IEnumerable<IOperation> indices)
    {
        return indices.Any(IsRange);
    }

    private static bool IsRange(IOperation index)
    {
        return index.Type is
            { Name: "Range", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };
    }

    /// <summary>
    ///     The value a field, property, event, element or method result is reached from, or null. For an
    ///     extension method that is the argument passed to its <c>this</c> parameter.
    /// </summary>
    private static IOperation? Receiver(IOperation operation)
    {
        return operation switch
        {
            IMemberReferenceOperation member => member.Instance,
            IArrayElementReferenceOperation element => element.ArrayReference,
            IImplicitIndexerReferenceOperation indexer => indexer.Instance,
            IInlineArrayAccessOperation inlineArray => inlineArray.Instance,
            IDynamicMemberReferenceOperation dynamicMember => dynamicMember.Instance,
            IDynamicIndexerAccessOperation dynamicIndexer => dynamicIndexer.Operation,
            IInvocationOperation invocation => invocation.Instance ?? ExtensionReceiver(invocation),
            _ => null
        };
    }

    private static IOperation? ExtensionReceiver(IInvocationOperation invocation)
    {
        if (!invocation.TargetMethod.IsExtensionMethod)
            return null;

        foreach (var argument in invocation.Arguments)
            if (argument.Parameter is { Ordinal: 0 })
                return argument.Value;

        return null;
    }

    /// <summary>True when the operation denotes storage elsewhere: a member that returns by reference.</summary>
    private static bool ReturnsByRef(IOperation operation)
    {
        return operation switch
        {
            IFieldReferenceOperation field => field.Field.RefKind != RefKind.None,
            IPropertyReferenceOperation property => property.Property.RefKind != RefKind.None,
            IInvocationOperation invocation => invocation.TargetMethod.RefKind != RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property } => property.RefKind !=
                RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IMethodSymbol method } =>
                method.RefKind != RefKind.None,
            _ => false
        };
    }

    /// <summary>
    ///     True when a value of <paramref name="type" /> may be a reference to an object: classes, interfaces,
    ///     arrays, delegates, <c>dynamic</c> and type parameters not known to be value types.
    /// </summary>
    private static bool MayBeReference(ITypeSymbol? type)
    {
        return type is { IsValueType: false, TypeKind: not TypeKind.Pointer and not TypeKind.FunctionPointer };
    }

    private static bool IsBoxing(IConversionOperation conversion)
    {
        return conversion.Operand.Type is { IsValueType: true } && MayBeReference(conversion.Type);
    }

    /// <summary>
    ///     The conditional access whose <c>WhenNotNull</c> part contains <paramref name="instance" />; its
    ///     <c>Operation</c> is the value the instance stands for.
    /// </summary>
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

    private static void Report(OperationAnalysisContext context, IOperation target, int start)
    {
        // The target expression as written: `(p.X) = 1` is reported at `(p.X)`.
        var syntax = target.Syntax;
        while (syntax.Parent is ParenthesizedExpressionSyntax parenthesized)
            syntax = parenthesized;

        var span = TextSpan.FromBounds(Math.Min(start, syntax.SpanStart), syntax.Span.End);
        context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(syntax.SyntaxTree, span)));
    }
}