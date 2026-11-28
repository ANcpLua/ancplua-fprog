using Microsoft.CodeAnalysis;

namespace ancplua_fprog.Shared;

/// <summary>Creates every rule's descriptor the same way (AGENTS.md, "Diagnostic conventions").</summary>
internal static class FpDescriptors
{
    private const string Category = "Functional";

    /// <summary>
    ///     A descriptor with category <c>Functional</c>, default severity <c>Warning</c>, enabled by default.
    ///     Pass <paramref name="id" /> as a constant: release tracking (RS2000) reads it at the call site.
    /// </summary>
    public static DiagnosticDescriptor Create(string id, string title, string messageFormat)
    {
        return new DiagnosticDescriptor(id, title, messageFormat, Category, DiagnosticSeverity.Warning, true);
    }
}