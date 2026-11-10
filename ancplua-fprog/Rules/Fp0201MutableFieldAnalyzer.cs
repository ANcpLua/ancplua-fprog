using System.Collections.Immutable;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0201: a field inside the functional core that is neither <c>readonly</c> nor <c>const</c>, reported at
/// each variable declarator name. Static, struct, <c>volatile</c>, fixed-size buffer and <c>ref readonly</c>
/// ref fields count. A field-like event counts too: it declares a writable backing field at its declarator.
/// Compiler-generated backing fields and enum members (constants) are exempt.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0201MutableFieldAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0201",
        "Do not declare mutable fields in the functional core",
        "Make field '{0}' readonly or const");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (context.Symbol is IFieldSymbol { IsConst: false, IsReadOnly: false } field)
            ReportIfInCore(context, field);
    }

    private static void AnalyzeEvent(SymbolAnalysisContext context)
    {
        if (context.Symbol is IEventSymbol { IsAbstract: false, IsExtern: false, IsPartialDefinition: false } @event
            && HasBackingField(@event, context.CancellationToken))
        {
            ReportIfInCore(context, @event);
        }
    }

    /// <summary>
    /// A field-like event (declared by a variable declarator, without accessors) that is not abstract,
    /// extern or a partial definition is backed by a compiler-generated, writable field.
    /// </summary>
    private static bool HasBackingField(IEventSymbol @event, CancellationToken cancellationToken)
    {
        foreach (var reference in @event.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken).IsKind(SyntaxKind.VariableDeclarator))
                return true;
        }

        return false;
    }

    private static void ReportIfInCore(SymbolAnalysisContext context, ISymbol symbol)
    {
        if (!CoreScope.IsDeclaredInSource(symbol) || !CoreScope.IsInCore(symbol))
            return;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax declarator)
                context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.Identifier.GetLocation(), symbol.Name));
        }
    }
}
