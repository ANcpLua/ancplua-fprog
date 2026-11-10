using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0101: a method or local function inside the functional core returns <c>void</c>. Reported at the
/// method or local function name, at every declaration of a partial method, and at the operator token of
/// a user-defined operator declared with a <c>void</c> return type (compound assignment, increment).
/// Constructors, destructors, accessors and lambdas have no declared return type and are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0101VoidMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0101",
        "Do not return void in the functional core",
        "'{0}' returns void; return a value instead");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.OperatorDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // Roslyn 5.9.0 runs this action for a node inside a C# 15 union body three times: in the pass of its
        // own member, and again in the passes of the union and of the union's container, which carry a type or
        // a namespace. Every node reported here lies inside a method, local-function host, operator, field,
        // property or primary constructor, so a pass that carries a type or a namespace is always an extra one.
        if (context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol)
            return;

        var (returnType, name) = context.Node switch
        {
            MethodDeclarationSyntax method => (method.ReturnType, method.Identifier),
            LocalFunctionStatementSyntax local => (local.ReturnType, local.Identifier),
            OperatorDeclarationSyntax op => (op.ReturnType, op.OperatorToken),
            _ => (null, default)
        };

        // Only the keyword declares a void return; C# rejects `System.Void` as a type (CS0673). Checking the
        // syntax first keeps the semantic model out of every method that returns a value.
        if (returnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
            return;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is not IMethodSymbol
            {
                ReturnsVoid: true
            } symbol)
        {
            return;
        }

        if (CoreScope.IsDeclaredInSource(symbol) && CoreScope.IsInCore(symbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), symbol.Name));
    }
}
