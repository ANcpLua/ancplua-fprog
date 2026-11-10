using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0103: a <c>ref</c> or <c>out</c> parameter inside the functional core (<c>in</c> and
/// <c>ref readonly</c> are allowed). Covers every parameter written in source: methods, constructors,
/// local functions, lambdas, delegate declarations, extension receivers and both halves of a partial
/// method. Reported at the parameter name.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0103RefOutParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0103",
        "Do not use ref or out parameters in the functional core",
        "Return the result instead of writing through the '{0}' parameter");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Parameter);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var syntax = (ParameterSyntax)context.Node;
        if (syntax.Identifier.IsMissing)
            return;

        if (context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not
            {
                RefKind: RefKind.Ref or RefKind.Out
            } parameter)
        {
            return;
        }

        if (!CoreScope.IsDeclaredInSource(parameter) || !CoreScope.IsInCore(parameter))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.Identifier.GetLocation(), parameter.Name));
    }
}
