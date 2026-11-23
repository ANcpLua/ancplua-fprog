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
///     FP0301: a <c>throw</c> statement or <c>throw</c> expression inside the functional core, including
///     a rethrow (<c>throw;</c>), reported at the <c>throw</c> keyword; and a call of a throw helper, reported at
///     the method name token. A throw helper is a static method whose name starts with <c>Throw</c> on a type
///     that derives from <c>System.Exception</c>, such as <c>ArgumentNullException.ThrowIfNull</c> or
///     <c>ObjectDisposedException.ThrowIf</c>: it throws without the keyword, and .NET's code fixes for
///     CA1510 to CA1513 rewrite <c>if (...) throw</c> into exactly these calls. In the .NET 11 framework every
///     such public method is a <c>ThrowIf*</c> guard. Deliberately not reported: instance helpers such as
///     <c>CancellationToken.ThrowIfCancellationRequested()</c>, <c>ExceptionDispatchInfo.Throw</c> (not an
///     exception type), <c>Debug.Fail</c>, helpers on user types that are not exceptions, and method groups of
///     a throw helper (a reference, not a throw).
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
        if (!IsThrowHelper(invocation.TargetMethod)
            || invocation.Syntax is not InvocationExpressionSyntax syntax
            || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var name = NameToken(syntax.Expression);
        if (name.IsKind(SyntaxKind.IdentifierToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation()));
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
        // Roslyn 5.9.0 runs a syntax node action inside the body of a C# 15 union declaration once with
        // the enclosing member, then again with the union type and with each declaration around it (a
        // type or a namespace). A throw is always executable code, so its genuine pass carries a member:
        // a method, accessor, constructor (also the primary constructor for a base argument), operator,
        // the field, property or event of an initializer, or the entry point of top-level statements.
        // A type or namespace as containing symbol is therefore one of the repeated passes.
        if (context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol
            || !CoreScope.IsInCore(context.ContainingSymbol))
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