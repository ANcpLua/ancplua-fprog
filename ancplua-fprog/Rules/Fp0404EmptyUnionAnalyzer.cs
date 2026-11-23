using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0404 detects explicit creation of an empty union struct.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0404EmptyUnionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0404",
        "Construct a union from a case",
        "Create '{0}' from one of its cases; an empty union value matches no case and makes a complete switch throw");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.DefaultValue, OperationKind.ObjectCreation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        if (!operation.IsImplicit && operation.Type is { TypeKind: TypeKind.Struct, IsUnion: true } union
            && operation is IDefaultValueOperation
                or IObjectCreationOperation { Constructor: { IsImplicitlyDeclared: true, Parameters.Length: 0 } })
            context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), union.Name));
    }
}
