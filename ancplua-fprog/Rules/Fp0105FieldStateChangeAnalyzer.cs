using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0105 reports writes to state held by a field or event of the core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0105FieldStateChangeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0105",
        "Do not change state held in a field of the functional core",
        "Return a new value instead of changing the state held in '{0}'");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        Mutators.Register(context, Rule, MutationOwner.Field);
    }
}
