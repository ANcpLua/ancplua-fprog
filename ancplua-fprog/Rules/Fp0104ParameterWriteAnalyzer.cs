using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0104 reports writes to data reached from a parameter of core code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0104ParameterWriteAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0104",
        "Do not write through a parameter in the functional core",
        "Leave the data reached from parameter '{0}' unchanged and return a changed copy instead: a 'with' expression for a record, a new immutable collection for elements");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        Mutators.Register(context, Rule, MutationOwner.Parameter);
    }
}
