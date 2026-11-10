using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0302: a call to a partial member inside the functional core. Partial members are every static
/// <c>Parse</c> method on a type in namespace <c>System</c>, <c>Enumerable.First/Last/Single/ElementAt</c>
/// (not the <c>OrDefault</c> forms) and reads of the indexer declared on <c>Dictionary&lt;,&gt;</c>,
/// <c>IDictionary&lt;,&gt;</c> or <c>IReadOnlyDictionary&lt;,&gt;</c>. Calls are reported at the method
/// name token, indexer reads at the whole element access (for <c>d?[key]</c> including the receiver).
/// Method groups are references, not calls, and are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0302PartialMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0302",
        "Do not call partial members in the functional core",
        "'{0}' fails for some inputs; use a total alternative such as TryParse, FirstOrDefault or TryGetValue");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!IsPartialMethod(method) || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        if (invocation.Syntax is not InvocationExpressionSyntax syntax)
            return;

        var name = NameToken(syntax.Expression);
        if (name.IsKind(SyntaxKind.IdentifierToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), method.Name));
    }

    /// <summary>The method name token of the invoked expression, also when it is parenthesized: <c>(int.Parse)(s)</c>.</summary>
    private static SyntaxToken NameToken(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => NameToken(parenthesized.Expression),
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier,
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier,
        SimpleNameSyntax simpleName => simpleName.Identifier,
        _ => default
    };

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (!reference.Property.IsIndexer
            || !IsDictionaryType(reference.Property.OriginalDefinition.ContainingType)
            || IsWriteOnlyTarget(reference)
            || !CoreScope.IsInCore(context.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, ElementAccessLocation(reference.Syntax), "this[]"));
    }

    private static bool IsPartialMethod(IMethodSymbol method) => method.Name switch
    {
        "Parse" => method.IsStatic && IsSystemNamespace(method.ContainingType?.ContainingNamespace),
        "First" or "Last" or "Single" or "ElementAt" => IsEnumerable(method.ContainingType),
        _ => false
    };

    /// <summary>True for <c>System.Linq.Enumerable</c> or a type nested in it (extension blocks).</summary>
    private static bool IsEnumerable(INamedTypeSymbol? type)
    {
        var outermost = type;
        while (outermost?.ContainingType is { } containing)
            outermost = containing;

        return outermost is { Name: "Enumerable", ContainingNamespace: { Name: "Linq" } linq }
               && IsSystemNamespace(linq.ContainingNamespace);
    }

    private static bool IsDictionaryType(INamedTypeSymbol? type)
        => type is
           {
               ContainingType: null,
               MetadataName: "Dictionary`2" or "IDictionary`2" or "IReadOnlyDictionary`2",
               ContainingNamespace:
               {
                   Name: "Generic",
                   ContainingNamespace: { Name: "Collections" } collections
               }
           }
           && IsSystemNamespace(collections.ContainingNamespace);

    private static bool IsSystemNamespace(INamespaceSymbol? ns)
        => ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>
    /// True when the indexer is only assigned: the target of <c>d[key] = v</c>, of an object initializer
    /// entry <c>[key] = v</c>, or an element of a deconstruction target <c>(d[a], d[b]) = ...</c>.
    /// Compound assignments, <c>??=</c>, <c>++</c> and <c>--</c> also read and are not write-only.
    /// </summary>
    private static bool IsWriteOnlyTarget(IOperation reference)
    {
        if (reference.Parent is ISimpleAssignmentOperation simple)
            return simple.Target == reference;

        var current = reference;
        while (current.Parent is ITupleOperation tuple)
            current = tuple;

        return current != reference
               && current.Parent is IDeconstructionAssignmentOperation deconstruction
               && deconstruction.Target == current;
    }

    /// <summary>
    /// The whole element access. When it starts with a binding (<c>?[key]</c> or <c>?.Map[key]</c>),
    /// the span is extended to the receiver of the enclosing conditional access, so <c>d?[key]</c>
    /// is reported as a whole.
    /// </summary>
    private static Location ElementAccessLocation(SyntaxNode syntax)
    {
        var start = syntax.SpanStart;
        var current = syntax;
        while (current.Parent is { } parent)
        {
            if (parent is ConditionalAccessExpressionSyntax conditional && conditional.WhenNotNull == current)
                start = parent.SpanStart;
            else if (parent.SpanStart != current.SpanStart)
                break;

            current = parent;
        }

        return Location.Create(syntax.SyntaxTree, TextSpan.FromBounds(start, syntax.Span.End));
    }
}
