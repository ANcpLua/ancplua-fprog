using System;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     Reports throw statements and expressions, static throw helpers on exception types, and calls to
///     methods with a <c>DoesNotReturn</c> contract in the functional core.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0301ThrowAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0301",
        "Do not throw in the functional core",
        "Return an error value instead of throwing");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!CoreScope.IsInCore(context.ContainingSymbol)
            || invocation.Syntax is not InvocationExpressionSyntax syntax
            || !(IsThrowHelper(invocation.TargetMethod) || DoesNotReturn(invocation.TargetMethod)))
            return;

        var name = NameToken(syntax.Expression);
        if (name.IsKind(SyntaxKind.IdentifierToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation()));
    }

    private static bool DoesNotReturn(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
            if (attribute.AttributeClass is
                {
                    MetadataName: "DoesNotReturnAttribute",
                    ContainingType: null,
                    ContainingNamespace:
                    {
                        Name: "CodeAnalysis",
                        ContainingNamespace:
                        {
                            Name: "Diagnostics",
                            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                        }
                    }
                })
                return true;

        return false;
    }

    private static bool IsThrowHelper(IMethodSymbol method)
    {
        return method.IsStatic
               && method.Name.StartsWith("Throw", StringComparison.Ordinal)
               && IsExceptionType(method.ContainingType);
    }

    private static bool IsExceptionType(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current is
                {
                    Name: "Exception", ContainingType: null,
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                })
                return true;

        return false;
    }

    /// <summary>The method name token of the invoked expression, also when it is parenthesized.</summary>
    private static SyntaxToken NameToken(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => NameToken(parenthesized.Expression),
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier,
            SimpleNameSyntax simpleName => simpleName.Identifier,
            _ => default
        };
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var keyword = context.Node switch
        {
            ThrowStatementSyntax statement => statement.ThrowKeyword,
            ThrowExpressionSyntax expression => expression.ThrowKeyword,
            _ => default
        };

        if (keyword.IsKind(SyntaxKind.ThrowKeyword))
            context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation()));
    }
}
