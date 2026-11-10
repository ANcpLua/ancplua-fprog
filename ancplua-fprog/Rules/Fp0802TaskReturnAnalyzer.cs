using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0802: a method or local function inside the functional core returns
/// <c>System.Threading.Tasks.Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c> or <c>ValueTask&lt;T&gt;</c>.
/// Reported at the return type as written. Both declarations of a partial method are reported, a
/// nullable task type (<c>Task?</c>, <c>ValueTask?</c>) is reported at the whole nullable type, and a
/// <c>ref</c> return is reported at the type after <c>ref</c> or <c>ref readonly</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0802TaskReturnAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0802",
        "Do not return a task from the functional core",
        "Return a plain value and keep asynchronous work in the shell");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var taskTypes = TaskTypes(start.Compilation);
            if (taskTypes.IsEmpty)
                return;

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, taskTypes),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement,
                SyntaxKind.OperatorDeclaration,
                SyntaxKind.ConversionOperatorDeclaration);
        });
    }

    private static ImmutableArray<INamedTypeSymbol> TaskTypes(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(4);
        foreach (var metadataName in new[]
                 {
                     "System.Threading.Tasks.Task",
                     "System.Threading.Tasks.Task`1",
                     "System.Threading.Tasks.ValueTask",
                     "System.Threading.Tasks.ValueTask`1"
                 })
        {
            if (compilation.GetTypeByMetadataName(metadataName) is { } type)
                builder.Add(type);
        }

        return builder.ToImmutable();
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> taskTypes)
    {
        var returnTypeSyntax = context.Node switch
        {
            MethodDeclarationSyntax method => method.ReturnType,
            LocalFunctionStatementSyntax localFunction => localFunction.ReturnType,
            OperatorDeclarationSyntax @operator => @operator.ReturnType,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type,
            _ => null
        };

        if (returnTypeSyntax is null
            || context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
                is not IMethodSymbol symbol
            || !CoreScope.IsDeclaredInSource(symbol)
            || !CoreScope.IsInCore(symbol)
            || !IsTaskType(symbol.ReturnType, taskTypes))
        {
            return;
        }

        var reported = returnTypeSyntax is RefTypeSyntax refType ? refType.Type : returnTypeSyntax;
        context.ReportDiagnostic(Diagnostic.Create(Rule, reported.GetLocation()));
    }

    private static bool IsTaskType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> taskTypes)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        foreach (var taskType in taskTypes)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, taskType))
                return true;
        }

        return false;
    }
}
