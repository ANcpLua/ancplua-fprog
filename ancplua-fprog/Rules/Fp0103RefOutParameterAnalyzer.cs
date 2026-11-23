using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0103: a <c>ref</c> or <c>out</c> parameter inside the functional core (<c>in</c> and
///     <c>ref readonly</c> are allowed). Covers every parameter written in source: methods, constructors
///     (including both parts of a partial constructor), local functions, lambdas, anonymous methods,
///     delegate declarations, extension receivers and both halves of a partial method. Reported at the
///     parameter name.
///     <para>
///         A semantic model action walks the whole tree. A syntax node action on <c>Parameter</c> is not enough:
///         the Roslyn 5.12.0 analyzer driver visits only the implementing part of a partial constructor.
///     </para>
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
        context.RegisterSemanticModelAction(Analyze);
    }

    private static void Analyze(SemanticModelAnalysisContext context)
    {
        var root = context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken);
        var nodes = context.FilterSpan is { } span ? root.DescendantNodes(span) : root.DescendantNodes();
        foreach (var node in nodes)
            if (node is ParameterSyntax syntax && IsCandidate(syntax))
                AnalyzeParameter(context, syntax);
    }

    /// <summary>
    ///     Cheap syntactic filter: a <c>ref</c> or <c>out</c> keyword (the symbol then tells <c>ref</c> from
    ///     <c>ref readonly</c>) and a written name to report at. A lambda parameter is never <c>ref</c> or
    ///     <c>out</c> without the keyword.
    /// </summary>
    private static bool IsCandidate(ParameterSyntax syntax)
    {
        return !syntax.Identifier.Span.IsEmpty
               && (syntax.Modifiers.Any(SyntaxKind.RefKeyword) || syntax.Modifiers.Any(SyntaxKind.OutKeyword));
    }

    private static void AnalyzeParameter(SemanticModelAnalysisContext context, ParameterSyntax syntax)
    {
        if (context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not
            {
                RefKind: RefKind.Ref or RefKind.Out
            } parameter)
            return;

        if (!CoreScope.IsDeclaredInSource(parameter) || !CoreScope.IsInCore(parameter))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.Identifier.GetLocation(), parameter.Name));
    }
}
