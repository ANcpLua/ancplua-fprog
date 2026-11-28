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
        "'{0}' is outside the functional core; take its result as a parameter or return a value that describes the effect for the shell to run, and mark a pure type [Fprog.FunctionalCore] or list a trusted library in fprog_pure_apis");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        EntryRule.Register(context, Rule, PurityClass.Shell);
    }
}
