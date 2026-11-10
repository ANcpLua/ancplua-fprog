using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>
/// Finds the Fprog marker attributes by full metadata name (<c>Fprog.FunctionalCoreAttribute</c>,
/// <c>Fprog.UnionAttribute</c>). The name is compared part by part, so a call allocates no strings,
/// and a copy of the attribute in any assembly counts.
/// </summary>
internal static class FprogAttributes
{
    public const string FunctionalCore = "FunctionalCoreAttribute";
    public const string Union = "UnionAttribute";

    private const string Namespace = "Fprog";

    /// <summary>True when one of <paramref name="attributes"/> is <c>Fprog.</c><paramref name="metadataName"/>.</summary>
    public static bool Contains(ImmutableArray<AttributeData> attributes, string metadataName)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is
                {
                    ContainingType: null,
                    ContainingNamespace: { Name: Namespace, ContainingNamespace.IsGlobalNamespace: true }
                } attributeClass
                && attributeClass.MetadataName == metadataName)
            {
                return true;
            }
        }

        return false;
    }
}
