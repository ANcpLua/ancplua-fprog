using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0803 prevents Shell implementations of core classes and interfaces.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0803ShellInheritanceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0803",
        "Keep implementations of core types in the functional core",
        "Keep the implementation of '{0}' in the core or use composition in the shell");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (CoreScope.IsInCore(type))
            return;

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(context.CancellationToken) is not BaseTypeDeclarationSyntax { BaseList: { } bases })
                continue;

#pragma warning disable RS1030 // The symbol callback needs the written base types, including aliases.
            var model = context.Compilation.GetSemanticModel(reference.SyntaxTree);
#pragma warning restore RS1030
            foreach (var baseType in bases.Types)
            {
                if (model.GetTypeInfo(baseType.Type, context.CancellationToken).Type is not INamedTypeSymbol inherited
                    || !CoreScope.IsInCore(inherited))
                    continue;

                var name = baseType.Type switch
                {
                    SimpleNameSyntax simple => simple.Identifier,
                    QualifiedNameSyntax qualified => qualified.Right.Identifier,
                    AliasQualifiedNameSyntax aliased => aliased.Name.Identifier,
                    _ => default
                };
                if (name.RawKind != 0)
                    context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), inherited.Name));
            }
        }
    }
}
