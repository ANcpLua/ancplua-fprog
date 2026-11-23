using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0802: a method or local function inside the functional core returns
///     <c>System.Threading.Tasks.Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c> or <c>ValueTask&lt;T&gt;</c>.
///     A class derived from <c>Task</c> or <c>Task&lt;T&gt;</c> and a type parameter constrained to a task type
///     count as task types.
///     Reported at the return type as written. Both declarations of a partial method are reported, a
///     nullable task type (<c>Task?</c>, <c>ValueTask?</c>) is reported at the whole nullable type, and a
///     <c>ref</c> return is reported at the type after <c>ref</c> or <c>ref readonly</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0802TaskReturnAnalyzer : DiagnosticAnalyzer
{
    private const int MaxConstraintDepth = 32;

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
            if (compilation.GetTypeByMetadataName(metadataName) is { } type)
                builder.Add(type);

        return builder.ToImmutable();
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> taskTypes)
    {
        // Roslyn 5.9.0 runs a node inside a C# 15 union body three times: for its own member, for the union
        // and for the union's container. Every real report comes from the pass of the method itself or of the
        // member that holds the local function (the primary constructor for a base argument), never from a
        // pass whose containing symbol is a type or a namespace.
        if (context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol)
            return;

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
            return;

        var reported = returnTypeSyntax is RefTypeSyntax refType ? refType.Type : returnTypeSyntax;
        context.ReportDiagnostic(Diagnostic.Create(Rule, reported.GetLocation()));
    }

    /// <summary>
    ///     True when <paramref name="type" /> is one of the task types, derives from one (a class deriving from
    ///     <c>Task</c> or <c>Task&lt;T&gt;</c> is a task), is a nullable of one, or is a type parameter
    ///     constrained to one. <paramref name="depth" /> bounds the walk through type parameter constraints so
    ///     that cyclic constraints in code that does not compile cannot recurse forever.
    /// </summary>
    private static bool IsTaskType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> taskTypes, int depth = 0)
    {
        if (depth > MaxConstraintDepth)
            return false;

        switch (type)
        {
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable:
                return IsTaskType(nullable.TypeArguments[0], taskTypes, depth + 1);
            case ITypeParameterSymbol typeParameter:
                foreach (var constraint in typeParameter.ConstraintTypes)
                    if (IsTaskType(constraint, taskTypes, depth + 1))
                        return true;

                return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
            foreach (var taskType in taskTypes)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, taskType))
                    return true;

        return false;
    }
}