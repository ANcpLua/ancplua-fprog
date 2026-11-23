using System.Collections.Immutable;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     FPS001: suppresses CS8509 on a <c>switch</c> expression over a union value that handles every case
///     with its own arm (<see cref="UnionSwitchCoverage.HandlesEveryCase" />). A switch whose value may be
///     null, and that has no arm matching null, keeps its warning: the compiler would not report the
///     missing null separately.
///     <para>
///         The union must also be visibly closed (<see cref="UnionModel.IsClosed" />): abstract, and without an
///         instance constructor that is not private, the record copy constructor aside. Otherwise <c>new Shape()</c>
///         or a type in another project that derives from the union reaches no arm and the switch throws
///         <c>SwitchExpressionException</c>, so CS8509 is right and stays. This matters most for a union from a
///         referenced assembly: FP0401 and FP0402 are reported only where the union is declared, so the consuming
///         project has no other warning. The check is kept out of <see cref="UnionSwitchCoverage.HandlesEveryCase" />
///         so FP0502, which reads the coverage, does not change.
///     </para>
///     <para>
///         Known and accepted: a record union cannot make its copy constructor private (CS8878), so another assembly
///         can still write <c>record Extra : Shape { public Extra(Shape original) : base(original) { } }</c> and
///         create a value no arm handles. FP0403 reports that type in its own project. Counting the copy constructor
///         would end suppression for every record union, so it is deliberately exempt.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fps001UnionSwitchSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Rule = new(
        "FPS001",
        "CS8509",
        "The switch expression handles every case of the union");

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [Rule];

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        UnionModel? model = null;
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id != Rule.SuppressedDiagnosticId
                || diagnostic.Location is not { IsInSource: true, SourceTree: { } tree } location)
                continue;

            var token = tree.GetRoot(context.CancellationToken).FindToken(location.SourceSpan.Start);
            if (!token.IsKind(SyntaxKind.SwitchKeyword) || token.Parent is not SwitchExpressionSyntax syntax)
                continue;

            var semanticModel = context.GetSemanticModel(tree);
            if (semanticModel.GetOperation(syntax, context.CancellationToken) is not ISwitchExpressionOperation
                operation)
                continue;

            model ??= new UnionModel(context.Compilation);
            if (model.TryGetCoverage(operation, out var coverage)
                && coverage.HandlesEveryCase
                && UnionModel.IsClosed(coverage.Union)
                && (!MayBeNull(semanticModel, syntax, operation, context.CancellationToken) || HasNullArm(operation)))
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
        }
    }

    /// <summary>
    ///     True when the switched value may be null as nullable analysis sees it. In code without nullable
    ///     annotations nothing is known, and the value counts as not null.
    /// </summary>
    private static bool MayBeNull(
        SemanticModel semanticModel,
        SwitchExpressionSyntax syntax,
        ISwitchExpressionOperation operation,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetTypeInfo(syntax.GoverningExpression, cancellationToken).Nullability.FlowState ==
               NullableFlowState.MaybeNull
               || operation.Value.Type?.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static bool HasNullArm(ISwitchExpressionOperation operation)
    {
        foreach (var arm in operation.Arms)
            if (arm.Guard is null && MatchesNull(arm.Pattern))
                return true;

        return false;
    }

    /// <summary>True when <paramref name="pattern" /> matches a null input.</summary>
    private static bool MatchesNull(IPatternOperation pattern)
    {
        return pattern switch
        {
            IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } } => true,
            IDiscardPatternOperation => true,
            IDeclarationPatternOperation { MatchesNull: true } => true,
            INegatedPatternOperation negated => !MatchesNull(negated.Pattern),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either
                => MatchesNull(either.LeftPattern) || MatchesNull(either.RightPattern),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both
                => MatchesNull(both.LeftPattern) && MatchesNull(both.RightPattern),
            _ => false
        };
    }
}