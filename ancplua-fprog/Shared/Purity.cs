using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Shared;

internal enum PurityClass
{
    Core,
    Async,
    Pure,
    Shell
}

/// <summary>Classifies types at the functional core boundary.</summary>
internal static class Purity
{
    private const string ConfigKey = "fprog_pure_apis";

    private static readonly ImmutableHashSet<string> PureNamespaces = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.ObjectModel",
        "System.Collections.Immutable",
        "System.Collections.Frozen",
        "System.Numerics",
        "System.Globalization",
        "System.Buffers.Binary",
        "System.Buffers.Text",
        "System.Diagnostics.CodeAnalysis");

    private static readonly ImmutableHashSet<string> SystemExceptions = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Activator", "AppContext", "AppDomain", "Console", "Environment", "GC", "Random", "TimeProvider",
        "OperatingSystem", "WeakReference", "Progress", "IProgress", "IServiceProvider", "IObservable",
        "IObserver", "IAsyncResult", "AsyncCallback", "MarshalByRefObject");

    private static readonly ImmutableHashSet<string> PureTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Linq.Enumerable",
        "System.Linq.ImmutableArrayExtensions",
        "System.Linq.IGrouping",
        "System.Linq.ILookup",
        "System.Linq.Lookup",
        "System.Linq.IOrderedEnumerable",
        "System.Runtime.CompilerServices.ITuple",
        "System.Runtime.CompilerServices.IUnion",
        "System.Runtime.CompilerServices.FormattableStringFactory",
        "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler",
        "System.Buffers.SearchValues");

    private static readonly ImmutableHashSet<string> AsyncTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Collections.Generic.IAsyncEnumerable",
        "System.Collections.Generic.IAsyncEnumerator",
        "System.IAsyncDisposable",
        "System.Linq.AsyncEnumerable");

    public static PurityClass Classify(ITypeSymbol? type, AnalyzerConfigOptionsProvider options, SyntaxTree tree)
    {
        if (CoreScope.IsInCore(type))
            return PurityClass.Core;

        // Array and nullable values carry the type of their elements or underlying value.
        if (type is IArrayTypeSymbol array)
            return Classify(array.ElementType, options, tree);
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return Classify(nullable.TypeArguments[0], options, tree);

        // Type parameters have no concrete target. Dynamic and pointers have their own rules.
        if (type is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error || named.IsAnonymousType)
            return PurityClass.Pure;

        var outer = named;
        while (outer.ContainingType is { } parent)
            outer = parent;

        var namespaceName = NamespaceName(outer);
        var name = QualifiedName(namespaceName, outer.Name);
        if (IsNamespaceOrChild(namespaceName, "System.Threading.Tasks") || AsyncTypes.Contains(name)
            || namespaceName == "System.Runtime.CompilerServices"
               && (named.Name.IndexOf("Await", StringComparison.Ordinal) >= 0
                   || named.Name.IndexOf("Async", StringComparison.Ordinal) >= 0))
            return PurityClass.Async;

        if (PureNamespaces.Contains(namespaceName) || IsNamespaceOrChild(namespaceName, "System.Text")
            || namespaceName == "System" && !SystemExceptions.Contains(outer.Name)
            || PureTypes.Contains(name)
            || IsConfiguredPure(named, namespaceName, options, tree))
            return PurityClass.Pure;

        return PurityClass.Shell;
    }

    private static bool IsConfiguredPure(
        INamedTypeSymbol type, string namespaceName, AnalyzerConfigOptionsProvider options, SyntaxTree tree)
    {
        if (!options.GetOptions(tree).TryGetValue(ConfigKey, out var value)
            && !options.GlobalOptions.TryGetValue(ConfigKey, out value))
            return false;

        var typeName = TypeName(type);
        foreach (var entry in value.Split(','))
        {
            var configured = entry.Trim();
            if (configured.Length > 0
                && (configured == typeName || IsNamespaceOrChild(namespaceName, configured)))
                return true;
        }

        return false;
    }

    private static string TypeName(INamedTypeSymbol type)
    {
        var qualifier = type.ContainingType is { } outer ? TypeName(outer) : NamespaceName(type);
        return QualifiedName(qualifier, type.Name);
    }

    private static string NamespaceName(INamedTypeSymbol type)
    {
        return type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
    }

    private static string QualifiedName(string qualifier, string name)
    {
        return qualifier.Length == 0 ? name : qualifier + "." + name;
    }

    private static bool IsNamespaceOrChild(string namespaceName, string prefix)
    {
        return namespaceName == prefix || namespaceName.Length > prefix.Length && namespaceName[prefix.Length] == '.'
            && namespaceName.StartsWith(prefix, StringComparison.Ordinal);
    }
}
