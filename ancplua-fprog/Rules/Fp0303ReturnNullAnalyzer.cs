using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     Reports null or default returned into a type that can hold null, nullable result annotations,
///     and the null-forgiving operator in the functional core.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0303ReturnNullAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0303",
        "Represent missing results explicitly in the functional core",
        "Use an option or result value to represent absence, or check the value before using it");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeReturn, OperationKind.Return, OperationKind.YieldReturn);
        context.RegisterSyntaxNodeAction(AnalyzeResultType, SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement,
            SyntaxKind.OperatorDeclaration, SyntaxKind.ConversionOperatorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNullForgiving, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void AnalyzeResultType(SyntaxNodeAnalysisContext context)
    {
        if (!IsMemberInCore(context.ContainingSymbol))
            return;

        var resultType = context.Node switch
        {
            MethodDeclarationSyntax method => method.ReturnType,
            LocalFunctionStatementSyntax local => local.ReturnType,
            OperatorDeclarationSyntax @operator => @operator.ReturnType,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type,
            _ => null
        };

        if (resultType is null)
            return;

        foreach (var nullable in resultType.DescendantNodesAndSelf().OfType<NullableTypeSyntax>())
            context.ReportDiagnostic(Diagnostic.Create(Rule, nullable.QuestionToken.GetLocation()));
    }

    private static void AnalyzeNullForgiving(SyntaxNodeAnalysisContext context)
    {
        var symbol = context.ContainingSymbol;
        if (!CoreScope.IsInCore(symbol)
            || symbol is INamedTypeSymbol && !SymbolEqualityComparer.Default.Equals(symbol, GetTypeAttributeOwner(context)))
            return;

        var syntax = (PostfixUnaryExpressionSyntax)context.Node;
        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.OperatorToken.GetLocation()));
    }

    private static ISymbol? GetTypeAttributeOwner(SyntaxNodeAnalysisContext context)
    {
        var owner = context.Node.FirstAncestorOrSelf<AttributeListSyntax>()?.Parent;
        if (owner is TypeParameterSyntax parameter)
            owner = parameter.Parent?.Parent;

        return owner is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
            ? context.SemanticModel.GetDeclaredSymbol(owner, context.CancellationToken)
            : null;
    }

    // Roslyn also visits a union body's syntax in passes for the surrounding type and namespace.
    private static bool IsMemberInCore(ISymbol? symbol)
    {
        return symbol is not (INamedTypeSymbol or INamespaceSymbol) && CoreScope.IsInCore(symbol);
    }

    private static void AnalyzeReturn(OperationAnalysisContext context)
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