using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0801 reports Shell values entering the functional core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0801ShellBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0801",
        "Keep Shell types and locks outside the functional core",
        "Use a core or Pure value in place of '{0}'");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        EntryRule.Register(context, Rule, PurityClass.Shell);
    }
}
