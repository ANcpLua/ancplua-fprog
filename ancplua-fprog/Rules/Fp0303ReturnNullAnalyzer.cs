using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0303: a <c>return</c> (explicit, expression-bodied, lambda or <c>yield return</c>) inside the
/// functional core whose value is a <c>null</c> literal or a <c>default</c> expression, converted to a
/// type that is not a non-nullable value type (pointers count as nullable). Conversions, conditional branches, switch-expression
/// arms, the right side of <c>??</c> and the value of <c>=</c> and <c>??=</c> are looked through. Reported at the literal or
/// expression. A <c>null</c> constant reached through a name, and <c>new int?()</c>, are not literals and are not reported.
/// The returned type is the type of the returned value: the <c>T</c> of an async <c>Task&lt;T&gt;</c>, the element type of
/// an iterator. Accessors, operators and conversion operators count as methods. The message names the option to return.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0303ReturnNullAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0303",
        "Do not return null or default in the functional core",
        "Return {0} instead of '{1}', so that the return type says the value can be missing");

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
        {
            return;
        }

        ReportNullOrDefault(context, value, type);
    }

    /// <summary>
    /// What to return instead: <c>an Option&lt;string&gt;</c> for <c>string?</c>, <c>an Option&lt;int&gt;</c> for
    /// <c>int?</c>. Pointers cannot be type arguments (CS0306), so they get a span or a plain value instead.
    /// </summary>
    private static string Suggestion(ITypeSymbol type)
    {
        switch (type)
        {
            case IPointerTypeSymbol { PointedAtType.SpecialType: not SpecialType.System_Void } pointer:
                return "a Span<" + pointer.PointedAtType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "> (empty when there is nothing)";
            case IPointerTypeSymbol or IFunctionPointerTypeSymbol:
                return "a value that is not a pointer (or the empty case of a union)";
        }

        var underlying = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        return "an Option<" + underlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "> (or the empty case of a union)";
    }

    private static bool CanBeNull(ITypeSymbol type)
        => !type.IsValueType
           || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
           || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static void ReportNullOrDefault(OperationAnalysisContext context, IOperation value, ITypeSymbol returnType)
    {
        switch (value)
        {
            case IConversionOperation conversion:
                ReportNullOrDefault(context, conversion.Operand, returnType);
                break;
            case IConditionalOperation { WhenFalse: { } whenFalse } conditional:
                ReportNullOrDefault(context, conditional.WhenTrue, returnType);
                ReportNullOrDefault(context, whenFalse, returnType);
                break;
            case ISwitchExpressionOperation switchExpression:
                foreach (var arm in switchExpression.Arms)
                    ReportNullOrDefault(context, arm.Value, returnType);
                break;
            case ICoalesceOperation coalesce:
                ReportNullOrDefault(context, coalesce.WhenNull, returnType);
                break;
            // `return x = null;` and `return x ??= null;` return the assigned null.
            case ISimpleAssignmentOperation { IsRef: false } assignment:
                ReportNullOrDefault(context, assignment.Value, returnType);
                break;
            case ICoalesceAssignmentOperation coalesceAssignment:
                ReportNullOrDefault(context, coalesceAssignment.Value, returnType);
                break;
            case ILiteralOperation { ConstantValue: { HasValue: true, Value: null } }:
                context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), Suggestion(returnType), "null"));
                break;
            case IDefaultValueOperation:
                context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), Suggestion(returnType), "default"));
                break;
        }
    }
}
