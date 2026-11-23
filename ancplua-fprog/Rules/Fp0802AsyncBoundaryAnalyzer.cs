using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0802 reports Async values and closures entering the functional core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0802AsyncBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0802",
        "Keep asynchronous work outside the functional core",
        "'{0}' is asynchronous; await it in the shell and pass the plain result to the core, or return a value that describes the work for the shell to run");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        EntryRule.Register(context, Rule, PurityClass.Async);
    }
}
