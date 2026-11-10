using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0203: a record or readonly struct in the functional core has a field or property whose type is an
/// array (including <c>System.Array</c>), <c>System.Text.StringBuilder</c>, or a type from
/// <c>System.Collections</c> or one of its namespaces other than <c>Immutable</c> and <c>Frozen</c>
/// (interfaces included). The type is searched through type arguments, pointed-at types and function
/// pointer signatures, so <c>ImmutableArray&lt;List&lt;int&gt;&gt;</c>, <c>KeyValuePair&lt;K, V&gt;?</c> and
/// <c>delegate*&lt;List&lt;int&gt;, int&gt;</c> count. Reported at the member name: the declarator name for
/// fields, the parameter name for positional record properties, and the <c>this</c> keyword for indexers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0203MutableCollectionMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0203",
        "Do not store mutable collections in records or readonly structs",
        "Use an immutable collection from System.Collections.Immutable or System.Collections.Frozen");

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
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static bool IsRecordOrReadOnlyStruct(INamedTypeSymbol? type)
        => type is { IsRecord: true } or { TypeKind: TypeKind.Struct, IsReadOnly: true };

    private static bool ContainsForbiddenType(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol:
                return true;
            case IPointerTypeSymbol pointer:
                return ContainsForbiddenType(pointer.PointedAtType);
            case IFunctionPointerTypeSymbol functionPointer:
                return ContainsForbiddenType(functionPointer.Signature.ReturnType)
                       || functionPointer.Signature.Parameters.Any(parameter => ContainsForbiddenType(parameter.Type));
            case INamedTypeSymbol named:
                if (IsForbiddenNamedType(named))
                    return true;

                for (var current = named; current is not null; current = current.ContainingType)
                {
                    foreach (var argument in current.TypeArguments)
                    {
                        if (ContainsForbiddenType(argument))
                            return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

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
