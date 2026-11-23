using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0502 reports cases without an unguarded case arm in a switch expression.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0502UnhandledUnionCaseAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0502",
        "Handle every case of a union in a switch expression",
        "The switch expression over '{0}' does not handle every case with an arm that has no 'when' clause: {1}");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.SwitchExpression);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var switchExpression = (ISwitchExpressionOperation)context.Operation;
        if (switchExpression.Syntax is not SwitchExpressionSyntax syntax
            || UnionModel.GetUnionType(switchExpression.Value.Type) is not { IsClosed: true } union)
            return;

        var missing = UnionModel.GetCases(union, context.CancellationToken)
            .Where(@case => !switchExpression.Arms.Any(
                arm => arm.Guard is null && NamesCase(context.Compilation, arm.Pattern, @case)))
            .ToArray();
        if (missing.Length == 0)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, syntax.SwitchKeyword.GetLocation(), union.Name, string.Join(", ", missing.Select(@case => @case.Name))));
    }

    private static bool NamesCase(Compilation compilation, IPatternOperation pattern, ITypeSymbol caseType)
    {
        if (pattern is IBinaryPatternOperation binary)
            return NamesCase(compilation, binary.LeftPattern, caseType)
                   || NamesCase(compilation, binary.RightPattern, caseType);

        var matchedType = pattern switch
        {
            ITypePatternOperation type => type.MatchedType,
            IDeclarationPatternOperation declaration => declaration.MatchedType,
            IRecursivePatternOperation recursive => recursive.MatchedType,
            _ => null
        };

        return matchedType is not null && compilation.ClassifyConversion(caseType, matchedType).IsIdentity;
    }
}
