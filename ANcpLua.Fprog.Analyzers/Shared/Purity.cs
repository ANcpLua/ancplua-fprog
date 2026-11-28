using System;
using System.Collections.Concurrent;
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

    private static readonly ImmutableHashSet<string> SystemExceptions = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Activator", "AppContext", "AppDomain", "Console", "Environment", "GC", "Random", "TimeProvider",
        "OperatingSystem", "WeakReference", "Progress", "IProgress", "IServiceProvider", "IObservable",
        "IObserver", "IAsyncResult", "AsyncCallback", "MarshalByRefObject");

    public static PurityClass Classify(ITypeSymbol? type, AnalyzerConfigOptionsProvider options, SyntaxTree tree)
    {
        if (CoreScope.IsInCore(type))
            return PurityClass.Core;

        if (UnderlyingType(type) is { } underlying)
            return Classify(underlying, options, tree);

        if (type is not INamedTypeSymbol named)
            return PurityClass.Pure;

        var builtIn = ClassifyNamed(named);
        return builtIn == PurityClass.Shell && IsConfiguredPure(named, ParseEntries(ConfigValue(options, tree)))
            ? PurityClass.Pure
            : builtIn;
    }

    internal sealed class Model(AnalyzerConfigOptionsProvider options)
    {
        private readonly ConcurrentDictionary<INamedTypeSymbol, PurityClass> _types = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<SyntaxTree, ConfiguredApis> _syntaxOptions = new();
        private readonly ConcurrentDictionary<string, ConfiguredApis> _parsedOptions = new(StringComparer.Ordinal);

        public CoreScope.Model Scope { get; } = new();

        public ConfiguredApis ForTree(SyntaxTree tree)
        {
            return _syntaxOptions.GetOrAdd(tree, ReadOptions);
        }

        private ConfiguredApis ReadOptions(SyntaxTree tree)
        {
            return _parsedOptions.GetOrAdd(ConfigValue(options, tree), static value => new ConfiguredApis(ParseEntries(value)));
        }

        public PurityClass Classify(ITypeSymbol type, ConfiguredApis configured)
        {
            if (type is not INamedTypeSymbol named)
            {
                if (Scope.Contains(type))
                    return PurityClass.Core;

                return UnderlyingType(type) is { } element ? Classify(element, configured) : PurityClass.Pure;
            }

            var definition = named.OriginalDefinition;
            if (!_types.TryGetValue(definition, out var builtIn))
                builtIn = _types.GetOrAdd(definition, Scope.Contains(definition) ? PurityClass.Core : ClassifyNamed(definition));

            if (builtIn != PurityClass.Core && UnderlyingType(named) is { } underlying)
                return Classify(underlying, configured);

            return builtIn == PurityClass.Shell && configured.Contains(definition) ? PurityClass.Pure : builtIn;
        }
    }

    internal sealed class ConfiguredApis(ImmutableArray<string> entries)
    {
        private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _types = new(SymbolEqualityComparer.Default);

        public bool Contains(INamedTypeSymbol type)
        {
            return !entries.IsEmpty && _types.GetOrAdd(type, IsPure);
        }

        private bool IsPure(INamedTypeSymbol type)
        {
            return IsConfiguredPure(type, entries);
        }
    }

    private static ITypeSymbol? UnderlyingType(ITypeSymbol? type)
    {
        // Array and nullable values carry the type of their elements or underlying value.
        return type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable => nullable.TypeArguments[0],
            _ => null
        };
    }

    private static PurityClass ClassifyNamed(INamedTypeSymbol named)
    {
        // Type parameters have no concrete target. Dynamic and pointers have their own rules.
        if (named.TypeKind == TypeKind.Error || named.IsAnonymousType)
            return PurityClass.Pure;

        var outer = named;
        while (outer.ContainingType is { } parent)
            outer = parent;

        return BuiltInNamespace(outer.ContainingNamespace) switch
        {
            NamespaceKind.Tasks => PurityClass.Async,
            NamespaceKind.System when outer.Name == "IAsyncDisposable" => PurityClass.Async,
            NamespaceKind.GenericCollections when outer.Name is "IAsyncEnumerable" or "IAsyncEnumerator" => PurityClass.Async,
            NamespaceKind.Linq when outer.Name == "AsyncEnumerable" => PurityClass.Async,
            NamespaceKind.CompilerServices when named.Name.IndexOf("Await", StringComparison.Ordinal) >= 0
                                                || named.Name.IndexOf("Async", StringComparison.Ordinal) >= 0 => PurityClass.Async,

            NamespaceKind.Pure or NamespaceKind.GenericCollections => PurityClass.Pure,
            NamespaceKind.System when !SystemExceptions.Contains(outer.Name) => PurityClass.Pure,
            NamespaceKind.Linq when outer.Name is "Enumerable" or "ImmutableArrayExtensions" or "IGrouping" or "ILookup"
                or "Lookup" or "IOrderedEnumerable" => PurityClass.Pure,
            NamespaceKind.CompilerServices when outer.Name is "ITuple" or "IUnion" or "FormattableStringFactory"
                or "DefaultInterpolatedStringHandler" => PurityClass.Pure,
            NamespaceKind.Buffers when outer.Name == "SearchValues" => PurityClass.Pure,
            _ => PurityClass.Shell
        };
    }

    private static NamespaceKind BuiltInNamespace(INamespaceSymbol ns)
    {
        if (IsSystemNamespace(ns))
            return NamespaceKind.System;

        var direct = true;
        for (var current = ns; !current.IsGlobalNamespace; current = current.ContainingNamespace, direct = false)
        {
            var parent = current.ContainingNamespace;
            if (IsSystemNamespace(parent))
            {
                if (current.Name == "Text")
                    return NamespaceKind.Pure;
                if (direct)
                    return current.Name switch
                    {
                        "Collections" or "Numerics" or "Globalization" => NamespaceKind.Pure,
                        "Buffers" => NamespaceKind.Buffers,
                        "Linq" => NamespaceKind.Linq,
                        _ => NamespaceKind.Other
                    };
                break;
            }

            if (parent.ContainingNamespace is not { } grandparent || !IsSystemNamespace(grandparent))
                continue;

            if (parent.Name == "Text")
                return NamespaceKind.Pure;
            if (parent.Name == "Threading" && current.Name == "Tasks")
                return NamespaceKind.Tasks;

            if (direct)
                return (parent.Name, current.Name) switch
                {
                    ("Collections", "Generic") => NamespaceKind.GenericCollections,
                    ("Collections", "ObjectModel" or "Immutable" or "Frozen")
                        or ("Buffers", "Binary" or "Text")
                        or ("Diagnostics", "CodeAnalysis") => NamespaceKind.Pure,
                    ("Runtime", "CompilerServices") => NamespaceKind.CompilerServices,
                    _ => NamespaceKind.Other
                };
        }

        return NamespaceKind.Other;
    }

    private static bool IsSystemNamespace(INamespaceSymbol ns)
    {
        return ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
    }

    private static string ConfigValue(AnalyzerConfigOptionsProvider options, SyntaxTree tree)
    {
        return options.GetOptions(tree).TryGetValue(ConfigKey, out var local)
            ? local
            : options.GlobalOptions.TryGetValue(ConfigKey, out var global) ? global : "";
    }

    private static ImmutableArray<string> ParseEntries(string value)
    {
        if (value.Length == 0)
            return [];

        var entries = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in value.Split(','))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length != 0)
                entries.Add(trimmed);
        }

        return entries.ToImmutable();
    }

    private static bool IsConfiguredPure(INamedTypeSymbol type, ImmutableArray<string> entries)
    {
        if (entries.IsEmpty)
            return false;

        var ns = type.ContainingNamespace;
        var namespaceName = ns.IsGlobalNamespace ? "" : ns.ToDisplayString();
        var typeName = TypeName(type, namespaceName);
        foreach (var configured in entries)
            if (configured == typeName || IsNamespaceOrChild(namespaceName, configured))
                return true;

        return false;
    }

    private static string TypeName(INamedTypeSymbol type, string namespaceName)
    {
        var qualifier = type.ContainingType is { } outer ? TypeName(outer, namespaceName) : namespaceName;
        return qualifier.Length == 0 ? type.Name : qualifier + "." + type.Name;
    }

    private static bool IsNamespaceOrChild(string namespaceName, string prefix)
    {
        return namespaceName == prefix || namespaceName.Length > prefix.Length && namespaceName[prefix.Length] == '.'
            && namespaceName.StartsWith(prefix, StringComparison.Ordinal);
    }

    private enum NamespaceKind
    {
        Other,
        System,
        Pure,
        GenericCollections,
        Linq,
        CompilerServices,
        Buffers,
        Tasks
    }
}
