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
///     FP0302: a call to a partial member inside the functional core. Partial members are every static
///     <c>Parse</c> method on a type in namespace <c>System</c>, <c>Enumerable.First/Last/Single/ElementAt</c>
///     (not the <c>OrDefault</c> forms; the same operators of <c>System.Linq.ImmutableArrayExtensions</c>,
///     which <c>xs.First()</c> on an <c>ImmutableArray&lt;T&gt;</c> binds to, count as well) and reads of the
///     indexer declared on <c>Dictionary&lt;,&gt;</c>, <c>IDictionary&lt;,&gt;</c> or
///     <c>IReadOnlyDictionary&lt;,&gt;</c>, or of the key indexer of any other dictionary declared in
///     <c>System.Collections</c> or below (<c>ImmutableDictionary&lt;,&gt;</c>, <c>FrozenDictionary&lt;,&gt;</c>,
///     <c>SortedDictionary&lt;,&gt;</c>, <c>ConcurrentDictionary&lt;,&gt;</c>, <c>ReadOnlyDictionary&lt;,&gt;</c>,
///     builders and so on): all of them throw <c>KeyNotFoundException</c> for a missing key. Indexers of user
///     types that implement a dictionary interface, of <c>JsonObject</c>, <c>ILookup&lt;,&gt;</c>,
///     <c>KeyedCollection&lt;,&gt;</c>, the non-generic dictionaries and lists are not reported unless read
///     through one of the three types above. Calls are reported at the method
///     name token, indexer reads at the whole element access (for <c>d?[key]</c> including the receiver).
///     A method group of a partial member (converted to a delegate, also through <c>new Func&lt;...&gt;(int.Parse)</c>,
///     or taken as a function pointer <c>&amp;int.Parse</c>) is reported at the method name token, because the
///     delegate fails the same way for every bad input it is given; invoking that delegate later is not reported
///     again, and <c>nameof(int.Parse)</c> only names the method and is not reported.
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
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
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

    /// <summary>
    ///     A method group converted to a delegate (<c>xs.Select(int.Parse)</c>, <c>new Func&lt;string, int&gt;(int.Parse)</c>)
    ///     or a function pointer (<c>&amp;int.Parse</c>): the method runs later for every value it is given, so it is
    ///     reported at its name like a call. Invoking the delegate is a call of <c>Invoke</c> and is not reported again.
    ///     The argument of <c>nameof</c> only names the method and is not reported.
    /// </summary>
    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        if (!IsPartialMethod(reference.Method) || IsInsideNameOf(reference) ||
            !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        if (reference.Syntax is not ExpressionSyntax syntax)
            return;

        var name = NameToken(syntax);
        if (name.IsKind(SyntaxKind.IdentifierToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), reference.Method.Name));
    }

    private static bool IsInsideNameOf(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
            if (parent is INameOfOperation)
                return true;

        return false;
    }

    /// <summary>The method name token of the invoked expression, also when it is parenthesized: <c>(int.Parse)(s)</c>.</summary>
    private static SyntaxToken NameToken(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => NameToken(parenthesized.Expression),
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier,
            SimpleNameSyntax simpleName => simpleName.Identifier,
            _ => default
        };
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (!IsDictionaryIndexer(reference.Property)
            || IsWriteOnlyTarget(reference)
            || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, ElementAccessLocation(reference.Syntax), "this[]"));
    }

    private static bool IsPartialMethod(IMethodSymbol method)
    {
        return method.Name switch
        {
            "Parse" => method.IsStatic && IsSystemNamespace(method.ContainingType?.ContainingNamespace),
            "First" or "Last" or "Single" or "ElementAt" => IsLinqOperatorClass(method.ContainingType),
            _ => false
        };
    }

    /// <summary>
    ///     True for <c>System.Linq.Enumerable</c>, for <c>System.Linq.ImmutableArrayExtensions</c> (the LINQ
    ///     operators an <c>ImmutableArray&lt;T&gt;</c> receiver binds to) or for a type nested in either (extension blocks).
    /// </summary>
    private static bool IsLinqOperatorClass(INamedTypeSymbol? type)
    {
        var outermost = type;
        while (outermost?.ContainingType is { } containing)
            outermost = containing;

        return outermost is
                   { Name: "Enumerable" or "ImmutableArrayExtensions", ContainingNamespace: { Name: "Linq" } linq }
               && IsSystemNamespace(linq.ContainingNamespace);
    }

    /// <summary>
    ///     True for the indexer declared on <c>Dictionary&lt;,&gt;</c>, <c>IDictionary&lt;,&gt;</c> or
    ///     <c>IReadOnlyDictionary&lt;,&gt;</c>, and for the key indexer of every other dictionary of the base class
    ///     library: a type declared in <c>System.Collections</c> or a namespace below it that implements
    ///     <c>IDictionary&lt;TKey, TValue&gt;</c> or <c>IReadOnlyDictionary&lt;TKey, TValue&gt;</c>, whose indexer has
    ///     exactly one parameter of that <c>TKey</c>. The test looks at the parameter, not at which interface member
    ///     the indexer implements, because <c>FrozenDictionary&lt;,&gt;</c> has a public
    ///     <c>ref readonly TValue this[TKey]</c> that implements neither interface indexer (those are explicit).
    ///     A user type with its own indexer is not covered: it may be total, and it is only reported when read
    ///     through one of the three types above.
    /// </summary>
    private static bool IsDictionaryIndexer(IPropertySymbol property)
    {
        if (!property.IsIndexer)
            return false;

        var definition = property.OriginalDefinition;
        var type = definition.ContainingType;
        if (IsDictionaryType(type))
            return true;

        if (type is null || definition.Parameters.Length != 1 || !IsInSystemCollections(type))
            return false;

        var parameterType = definition.Parameters[0].Type;
        foreach (var implemented in type.AllInterfaces)
            if (IsDictionaryInterface(implemented.OriginalDefinition)
                && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], parameterType))
                return true;

        return false;
    }

    /// <summary>True when the type, or the outermost type it is nested in, is declared in <c>System.Collections</c> or below.</summary>
    private static bool IsInSystemCollections(INamedTypeSymbol type)
    {
        for (var ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
            if (ns is { Name: "Collections" } && IsSystemNamespace(ns.ContainingNamespace))
                return true;

        return false;
    }

    private static bool IsDictionaryInterface(INamedTypeSymbol type)
    {
        return type.MetadataName is "IDictionary`2" or "IReadOnlyDictionary`2" && IsDictionaryType(type);
    }

    private static bool IsDictionaryType(INamedTypeSymbol? type)
    {
        return type is
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
    }

    private static bool IsSystemNamespace(INamespaceSymbol? ns)
    {
        return ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
    }

    /// <summary>
    ///     True when the indexer is only assigned: the target of <c>d[key] = v</c>, of an object initializer
    ///     entry <c>[key] = v</c>, or an element of a deconstruction target <c>(d[a], d[b]) = ...</c>.
    ///     Compound assignments, <c>??=</c>, <c>++</c> and <c>--</c> also read and are not write-only.
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
    ///     The whole element access. When it starts with a binding (<c>?[key]</c> or <c>?.Map[key]</c>),
    ///     the span is extended to the receiver of the enclosing conditional access, so <c>d?[key]</c>
    ///     is reported as a whole.
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