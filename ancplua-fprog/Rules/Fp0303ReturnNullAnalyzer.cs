using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0303: a <c>return</c> (explicit, expression-bodied, lambda or <c>yield return</c>) inside the
///     functional core whose value is a <c>null</c> literal or a <c>default</c> expression, converted to a
///     type that is not a non-nullable value type (pointers count as nullable). Conversions, conditional branches,
///     switch-expression
///     arms, the right side of <c>??</c> and the value of <c>=</c> and <c>??=</c> are looked through. Reported at the
///     literal or
///     expression. A <c>null</c> constant reached through a name, and <c>new int?()</c>, are not literals and are not
///     reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0303ReturnNullAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0303",
        "Do not return null or default in the functional core",
        "Return an option or result value instead of null or default");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Return, OperationKind.YieldReturn);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (context.Operation is not IReturnOperation { ReturnedValue: { Type: { } type } value }
            || !CanBeNull(type)
            || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        ReportNullOrDefault(context, value);
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        return !type.IsValueType
               || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
               || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static void ReportNullOrDefault(OperationAnalysisContext context, IOperation value)
    {
        switch (value)
        {
            case IConversionOperation conversion:
                ReportNullOrDefault(context, conversion.Operand);
                break;
            case IConditionalOperation { WhenFalse: { } whenFalse } conditional:
                ReportNullOrDefault(context, conditional.WhenTrue);
                ReportNullOrDefault(context, whenFalse);
                break;
            case ISwitchExpressionOperation switchExpression:
                foreach (var arm in switchExpression.Arms)
                    ReportNullOrDefault(context, arm.Value);
                break;
            case ICoalesceOperation coalesce:
                ReportNullOrDefault(context, coalesce.WhenNull);
                break;
            // `return x = null;` and `return x ??= null;` return the assigned null.
            case ISimpleAssignmentOperation { IsRef: false } assignment:
                ReportNullOrDefault(context, assignment.Value);
                break;
            case ICoalesceAssignmentOperation coalesceAssignment:
                ReportNullOrDefault(context, coalesceAssignment.Value);
                break;
            case ILiteralOperation { ConstantValue: { HasValue: true, Value: null } }:
            case IDefaultValueOperation:
                context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation()));
                break;
        }
    }
}