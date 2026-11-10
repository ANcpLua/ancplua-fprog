using System;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0104: inside the functional core, an assignment, compound assignment, <c>??=</c>, deconstruction
/// assignment, event subscription, <c>++</c> or <c>--</c> whose target is a field, property, event or
/// element reached from a parameter whose type may be a reference (class, interface, array, delegate,
/// <c>dynamic</c>, type parameter not known to be a value type) or from a <c>ref</c>/<c>out</c>
/// parameter. The chain from the parameter to the target may pass through fields, properties, indexers,
/// array elements, casts, null-conditional access and method calls. Writes through a struct parameter
/// passed by value, <c>in</c> or <c>ref readonly</c> (<c>Span&lt;T&gt;</c> and structs holding references
/// included) are not reported. Writes into a boxed copy are not reported unless the chain above the box
/// reads a reference or passes through a member that returns by reference. Reported at the whole
/// assignment target expression, including enclosing parentheses.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0104ParameterWriteAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0104",
        "Do not write through a parameter in the functional core",
        "Return a new value instead of mutating the object passed in");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            Analyze,
            OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment,
            OperationKind.EventAssignment,
            OperationKind.Increment,
            OperationKind.Decrement);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        switch (context.Operation)
        {
            case IDeconstructionAssignmentOperation deconstruction:
                Check(context, deconstruction.Target, rebinds: false);
                break;
            case ISimpleAssignmentOperation simple:
                // `x.F = ref y` rebinds the ref field F; it writes nothing through the storage F referred to.
                Check(context, simple.Target, simple.IsRef);
                break;
            case IAssignmentOperation assignment:
                Check(context, assignment.Target, rebinds: false);
                break;
            case IEventAssignmentOperation eventAssignment:
                Check(context, eventAssignment.EventReference, rebinds: false);
                break;
            case IIncrementOrDecrementOperation increment:
                Check(context, increment.Target, rebinds: false);
                break;
        }
    }

    private static void Check(OperationAnalysisContext context, IOperation target, bool rebinds)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
                Check(context, element, rebinds: false);

            return;
        }

        var shared = !rebinds && ReturnsByRef(target);
        var start = target.Syntax.SpanStart;
        var receiver = Receiver(target);
        while (receiver is not null)
        {
            if (receiver is IConversionOperation boxing && IsBoxing(boxing))
            {
                // The box is a fresh copy: a write into it reaches the caller only through a reference
                // read further up the chain.
                if (!shared)
                    return;

                receiver = boxing.Operand;
                continue;
            }

            shared |= ReturnsByRef(receiver) || MayBeReference(receiver.Type);
            switch (receiver)
            {
                case IParameterReferenceOperation { Parameter: var parameter }:
                    // A struct parameter (Span<T> included) is not a reference-type parameter unless ref/out.
                    if (MayBeReference(parameter.Type) || parameter.RefKind is RefKind.Ref or RefKind.Out)
                        Report(context, target, start);
                    return;
                case IConditionalAccessInstanceOperation instance:
                    var access = EnclosingAccess(instance);
                    if (access is null)
                        return;
                    start = access.Syntax.SpanStart;
                    receiver = access.Operation;
                    break;
                case IConversionOperation conversion:
                    receiver = conversion.Operand;
                    break;
                default:
                    receiver = Receiver(receiver);
                    break;
            }
        }
    }

    /// <summary>
    /// The value a field, property, event, element or method result is reached from, or null. For an
    /// extension method that is the argument passed to its <c>this</c> parameter.
    /// </summary>
    private static IOperation? Receiver(IOperation operation) => operation switch
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

    private static IOperation? ExtensionReceiver(IInvocationOperation invocation)
    {
        if (!invocation.TargetMethod.IsExtensionMethod)
            return null;

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is { Ordinal: 0 })
                return argument.Value;
        }

        return null;
    }

    /// <summary>True when the operation denotes storage elsewhere: a member that returns by reference.</summary>
    private static bool ReturnsByRef(IOperation operation) => operation switch
    {
        IFieldReferenceOperation field => field.Field.RefKind != RefKind.None,
        IPropertyReferenceOperation property => property.Property.RefKind != RefKind.None,
        IInvocationOperation invocation => invocation.TargetMethod.RefKind != RefKind.None,
        IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property } => property.RefKind != RefKind.None,
        IImplicitIndexerReferenceOperation { IndexerSymbol: IMethodSymbol method } => method.RefKind != RefKind.None,
        _ => false
    };

    /// <summary>
    /// True when a value of <paramref name="type"/> may be a reference to an object: classes, interfaces,
    /// arrays, delegates, <c>dynamic</c> and type parameters not known to be value types.
    /// </summary>
    private static bool MayBeReference(ITypeSymbol? type)
        => type is { IsValueType: false, TypeKind: not TypeKind.Pointer and not TypeKind.FunctionPointer };

    private static bool IsBoxing(IConversionOperation conversion)
        => conversion.Operand.Type is { IsValueType: true } && MayBeReference(conversion.Type);

    /// <summary>
    /// The conditional access whose <c>WhenNotNull</c> part contains <paramref name="instance"/>; its
    /// <c>Operation</c> is the value the instance stands for.
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
