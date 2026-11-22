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
/// (not the <c>OrDefault</c> forms; the same operators of <c>System.Linq.ImmutableArrayExtensions</c>,
/// which <c>xs.First()</c> on an <c>ImmutableArray&lt;T&gt;</c> binds to, count as well) and reads of the
/// indexer declared on <c>Dictionary&lt;,&gt;</c>, <c>IDictionary&lt;,&gt;</c> or
/// <c>IReadOnlyDictionary&lt;,&gt;</c>, including every indexer that implements the indexer of
/// <c>IDictionary&lt;,&gt;</c> or <c>IReadOnlyDictionary&lt;,&gt;</c> (stricter reading: <c>ConcurrentDictionary</c>,
/// <c>SortedDictionary</c>, <c>ImmutableDictionary</c> throw on a missing key as well), and the <c>ref readonly</c>
/// key indexer of a dictionary type such as <c>FrozenDictionary&lt;,&gt;</c>.
/// <c>T.Parse</c> on a type parameter constrained to <c>System.IParsable&lt;T&gt;</c> binds to
/// <c>System.IParsable&lt;T&gt;.Parse</c>, a static method on a type in <c>System</c>, and is reported. Calls are reported at the method
/// name token, indexer reads at the whole element access (for <c>d?[key]</c> including the receiver).
/// Method groups are references, not calls, and are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0302PartialMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0302",
        "Do not call partial members in the functional core",
        "'{0}' throws for some inputs; write '{1}' instead and handle the failure case as a value");

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
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), method.Name, TotalAlternative(method.Name)));
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

    /// <summary>The total member to write instead of the partial one.</summary>
    private static string TotalAlternative(string name) => name switch
    {
        "Parse" => "TryParse(text, out var value)",
        "ElementAt" => "ElementAtOrDefault(index)",
        _ => name + "OrDefault()"
    };

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (!reference.Property.IsIndexer
            || IsWriteOnlyTarget(reference)
            || !IsDictionaryIndexer(reference.Property)
            || !CoreScope.IsInCore(context.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, ElementAccessLocation(reference.Syntax), "this[key]", "TryGetValue(key, out var value)"));
    }

    /// <summary>
    /// True for the indexer declared on one of the dictionary types, or for an indexer that implements
    /// the indexer of <c>IDictionary&lt;,&gt;</c> or <c>IReadOnlyDictionary&lt;,&gt;</c>, or a <c>ref readonly</c>
    /// indexer keyed by <c>TKey</c> on a type that implements one of them.
    /// </summary>
    private static bool IsDictionaryIndexer(IPropertySymbol indexer)
    {
        if (IsDictionaryType(indexer.OriginalDefinition.ContainingType))
            return true;

        var type = indexer.ContainingType;
        if (type is null)
            return false;

        foreach (var contract in type.AllInterfaces)
        {
            if (!IsDictionaryType(contract.OriginalDefinition))
                continue;

            // FrozenDictionary<,>: the public indexer returns by ref and implements the interface indexer
            // explicitly; it is the same key lookup and throws on a missing key as well.
            if (indexer is { ReturnsByRefReadonly: true, Parameters.Length: 1 }
                && SymbolEqualityComparer.Default.Equals(indexer.Parameters[0].Type, contract.TypeArguments[0]))
            {
                return true;
            }

            foreach (var member in contract.GetMembers("this[]"))
            {
                if (member is IPropertySymbol { IsIndexer: true }
                    && SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), indexer))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPartialMethod(IMethodSymbol method) => method.Name switch
    {
        "Parse" => method.IsStatic && IsSystemNamespace(method.ContainingType?.ContainingNamespace),
        "First" or "Last" or "Single" or "ElementAt" => IsLinqOperatorClass(method.ContainingType),
        _ => false
    };

    /// <summary>
    /// True for <c>System.Linq.Enumerable</c>, for <c>System.Linq.ImmutableArrayExtensions</c> (the LINQ
    /// operators an <c>ImmutableArray&lt;T&gt;</c> receiver binds to) or for a type nested in either (extension blocks).
    /// </summary>
    private static bool IsLinqOperatorClass(INamedTypeSymbol? type)
    {
        var outermost = type;
        while (outermost?.ContainingType is { } containing)
            outermost = containing;

        return outermost is { Name: "Enumerable" or "ImmutableArrayExtensions", ContainingNamespace: { Name: "Linq" } linq }
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
