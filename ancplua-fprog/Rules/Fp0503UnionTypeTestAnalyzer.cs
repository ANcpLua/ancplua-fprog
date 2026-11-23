using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0503 requires matching union cases in a switch.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0503UnionTypeTestAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0503",
        "Match union cases in a switch",
        "Match the cases of '{0}' in a switch");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.IsType, OperationKind.IsPattern, OperationKind.Conversion);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        var value = operation switch
        {
            IIsTypeOperation test => test.ValueOperand,
            IIsPatternOperation test when NamesType(test.Pattern) => test.Value,
            IConversionOperation { IsTryCast: true, Syntax: BinaryExpressionSyntax syntax } conversion
                when syntax.IsKind(SyntaxKind.AsExpression) => conversion.Operand,
            _ => null
        };
        if (value is null || UnionModel.GetUnionOfExpression(value, context.Compilation) is not { } union)
            return;

        // An is-expression without designation can have an implicit pattern spanning the whole expression.
        var pattern = operation.Syntax switch
        {
            IsPatternExpressionSyntax test => (SyntaxNode)test.Pattern,
            BinaryExpressionSyntax test => test.Right,
            _ => null
        };
        if (pattern is not null)
            context.ReportDiagnostic(Diagnostic.Create(Rule, pattern.GetLocation(), union.Name));
    }

    private static bool NamesType(IPatternOperation pattern)
    {
        return pattern.DescendantsAndSelf().Any(static part => part is ITypePatternOperation
            or IDeclarationPatternOperation { MatchesNull: false }
            or IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: not null } });
    }
}
