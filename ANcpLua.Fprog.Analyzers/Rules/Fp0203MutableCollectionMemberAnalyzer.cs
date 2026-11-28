using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0203 reports mutable collections and writable memory held by a record or readonly struct in core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0203MutableCollectionMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0203",
        "Do not store mutable collections or memory in records or readonly structs",
        "Hold an immutable collection instead: ImmutableArray<T>, ImmutableList<T>, ImmutableDictionary<TKey, TValue> or a frozen collection, and string in place of StringBuilder");

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
            || !HasMutableType(type))
            return;

        var location = symbol.Locations.IsEmpty ? null : symbol.Locations[0];
        if (location is { IsInSource: true })
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static bool IsRecordOrReadOnlyStruct(INamedTypeSymbol? type)
    {
        return type is { IsRecord: true } or { TypeKind: TypeKind.Struct, IsReadOnly: true };
    }

    private static bool HasMutableType(ITypeSymbol type)
    {
        return type switch
        {
            IArrayTypeSymbol => true,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                => HasMutableType(nullable.TypeArguments[0]),
            INamedTypeSymbol named => IsForbiddenNamedType(named),
            _ => false
        };
    }

    private static bool IsForbiddenNamedType(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Array || Mutators.IsStringBuilder(type)
            || Mutators.IsSystemType(type, "Memory", 1) || Mutators.IsSystemType(type, "ArraySegment", 1))
            return true;

        var ns = type.ContainingNamespace;
        return Mutators.IsCollectionsNamespace(ns)
               || Mutators.IsCollectionsChild(ns, "Generic")
               || Mutators.IsCollectionsChild(ns, "ObjectModel")
               || (Mutators.IsNestedBuilder(type) && Mutators.IsCollectionsChild(ns, "Immutable"));
    }
}
