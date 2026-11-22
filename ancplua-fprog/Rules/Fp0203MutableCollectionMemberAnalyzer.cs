using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0203: a record or readonly struct in the functional core has a field or property whose type is an
/// array (including <c>System.Array</c>), <c>System.Text.StringBuilder</c>, or a type from
/// <c>System.Collections</c> or one of its namespaces other than <c>Immutable</c> and <c>Frozen</c>
/// (interfaces included). Only the member's own type is judged, after unwrapping <c>Nullable&lt;T&gt;</c>, so
/// <c>KeyValuePair&lt;K, V&gt;?</c> counts while <c>ImmutableArray&lt;List&lt;int&gt;&gt;</c> and
/// <c>Func&lt;IEnumerable&lt;int&gt;&gt;</c> do not. Reported at the member name: the declarator name for
/// fields, the parameter name for positional record properties, and the <c>this</c> keyword for indexers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0203MutableCollectionMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0203",
        "Do not store mutable collections in records or readonly structs",
        "Change the type of '{0}' from '{1}' to {2}; build it with ToImmutableArray(), ToImmutableDictionary() or ToFrozenDictionary() and return a new value instead of mutating it");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        var type = symbol switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };

        if (type is null
            || !IsRecordOrReadOnlyStruct(symbol.ContainingType)
            || !CoreScope.IsDeclaredInSource(symbol)
            || !CoreScope.IsInCore(symbol)
            || !ContainsForbiddenType(type))
        {
            return;
        }

        var location = symbol.Locations.IsEmpty ? null : symbol.Locations[0];
        if (location is { IsInSource: true })
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                symbol.Name,
                Display(type),
                Suggest(Unwrap(type))));
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    /// <summary>The immutable replacement the message names for a forbidden member type.</summary>
    private static string Suggest(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return $"ImmutableArray<{Display(array.ElementType)}>";

        if (type is not INamedTypeSymbol named)
            return DefaultSuggestion;

        if (named.Name == "StringBuilder")
            return "string";

        var arguments = named.TypeArguments;
        switch (named.Name)
        {
            case "Dictionary" or "IDictionary" or "IReadOnlyDictionary" or "ConcurrentDictionary"
                or "SortedDictionary" or "SortedList" or "ReadOnlyDictionary" when arguments.Length == 2:
                return $"ImmutableDictionary<{Display(arguments[0])}, {Display(arguments[1])}> "
                       + $"or FrozenDictionary<{Display(arguments[0])}, {Display(arguments[1])}>";
            case "HashSet" or "ISet" or "IReadOnlySet" or "SortedSet" when arguments.Length == 1:
                return $"ImmutableHashSet<{Display(arguments[0])}> or FrozenSet<{Display(arguments[0])}>";
            case "Queue" or "ConcurrentQueue" when arguments.Length == 1:
                return $"ImmutableQueue<{Display(arguments[0])}>";
            case "Stack" or "ConcurrentStack" when arguments.Length == 1:
                return $"ImmutableStack<{Display(arguments[0])}>";
        }

        return arguments.Length == 1 && named.ContainingType is null
            ? $"ImmutableArray<{Display(arguments[0])}>"
            : DefaultSuggestion;
    }

    private const string DefaultSuggestion
        = "an immutable type such as ImmutableArray<T>, ImmutableList<T> or ImmutableDictionary<TKey, TValue>";

    private static string Display(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    private static bool IsRecordOrReadOnlyStruct(INamedTypeSymbol? type)
        => type is { IsRecord: true } or { TypeKind: TypeKind.Struct, IsReadOnly: true };

    private static bool ContainsForbiddenType(ITypeSymbol type)
        => type switch
        {
            IArrayTypeSymbol => true,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                => ContainsForbiddenType(nullable.TypeArguments[0]),
            INamedTypeSymbol named => IsForbiddenNamedType(named),
            _ => false
        };

    private static bool IsForbiddenNamedType(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Array)
            return true;

        var ns = type.ContainingNamespace;
        if (type.Name == "StringBuilder" && IsSystemChild(ns, "Text"))
            return true;

        INamespaceSymbol? child = null;
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            if (IsSystemChild(current, "Collections"))
                return child is null || (child.Name != "Immutable" && child.Name != "Frozen");

            child = current;
        }

        return false;
    }

    private static bool IsSystemChild(INamespaceSymbol? ns, string name)
        => ns is { ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
           && ns.Name == name;
}
