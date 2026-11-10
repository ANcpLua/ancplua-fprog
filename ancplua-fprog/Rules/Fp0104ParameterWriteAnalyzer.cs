using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0104: inside the functional core, an assignment, compound assignment, <c>??=</c>, deconstruction
/// assignment, event subscription, <c>++</c> or <c>--</c> whose target is a field, property, event or
/// element reached from a parameter that is not known to be a value type (or that is passed by
/// <c>ref</c>/<c>out</c>). The chain from the parameter to the target may pass through fields,
/// properties, indexers, array elements, casts, null-conditional access and method calls.
/// Reported at the whole assignment target expression.
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

        var target = context.Operation switch
        {
            IDeconstructionAssignmentOperation deconstruction => deconstruction.Target,
            IAssignmentOperation assignment => assignment.Target,
            IEventAssignmentOperation eventAssignment => eventAssignment.EventReference,
            IIncrementOrDecrementOperation increment => increment.Target,
            _ => null
        };

        if (target is not null)
            Check(context, target);
    }

    private static void Check(OperationAnalysisContext context, IOperation target)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
                Check(context, element);

            return;
        }

        var receiver = Receiver(target);
        if (receiver is null)
            return;

        var start = target.Syntax.SpanStart;
        while (true)
        {
            switch (receiver)
            {
                case IParameterReferenceOperation { Parameter: var parameter }:
                    if (IsShared(parameter))
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
                case IInvocationOperation invocation:
                    receiver = invocation.Instance
                               ?? (invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0
                                   ? invocation.Arguments[0].Value
                                   : null);
                    break;
                default:
                    receiver = Receiver(receiver);
                    break;
            }

            if (receiver is null)
                return;
        }
    }

    /// <summary>The value a field, property, event or element access is reached from, or null.</summary>
    private static IOperation? Receiver(IOperation operation) => operation switch
    {
        IMemberReferenceOperation member => member.Instance,
        IArrayElementReferenceOperation element => element.ArrayReference,
        IImplicitIndexerReferenceOperation indexer => indexer.Instance,
        IInlineArrayAccessOperation inlineArray => inlineArray.Instance,
        IDynamicMemberReferenceOperation dynamicMember => dynamicMember.Instance,
        IDynamicIndexerAccessOperation dynamicIndexer => dynamicIndexer.Operation,
        _ => null
    };

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

    /// <summary>
    /// True when a write through <paramref name="parameter"/> is visible to the caller: its type may be
    /// a reference type (classes, interfaces, arrays, delegates, unconstrained type parameters), or it
    /// is passed by <c>ref</c> or <c>out</c>.
    /// </summary>
    private static bool IsShared(IParameterSymbol parameter)
        => parameter.RefKind is RefKind.Ref or RefKind.Out
           || parameter.Type is { IsValueType: false, TypeKind: not TypeKind.Pointer and not TypeKind.FunctionPointer };

    private static void Report(OperationAnalysisContext context, IOperation target, int start)
    {
        var syntax = target.Syntax;
        var location = Location.Create(syntax.SyntaxTree, TextSpan.FromBounds(start, syntax.Span.End));
        context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }
}
