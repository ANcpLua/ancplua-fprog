using System.Collections.Generic;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0701: inside the functional core, a lambda, anonymous method or local function captures a local
/// or parameter that is written anywhere in the enclosing member other than by its declaration:
/// assignment, compound or coalescing assignment, <c>++</c>/<c>--</c>, deconstruction, a <c>ref</c> or
/// <c>out</c> argument, a <c>foreach</c> into existing variables, or a field or property write through a
/// value-type variable. Reported once per variable and closure, at the first reference to the variable
/// inside the closure; a reference that is the first one of several nested closures is reported once.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0701MutableCaptureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0701",
        "Do not capture a variable that is reassigned",
        "The closure captures '{0}', which is assigned after its declaration");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(Analyze);
    }

    private static void Analyze(OperationBlockAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.OwningSymbol))
            return;

        var written = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var closures = new List<IOperation>();
        foreach (var block in context.OperationBlocks)
            Collect(block, written, closures);

        if (written.Count == 0 || closures.Count == 0)
            return;

        var reported = new HashSet<SyntaxNode>();
        foreach (var closure in closures)
        {
            var firstUses = new Dictionary<ISymbol, IOperation>(SymbolEqualityComparer.Default);
            FindCapturedUses(closure, closure.Syntax, written, firstUses);
            foreach (var use in firstUses)
            {
                if (reported.Add(use.Value.Syntax))
                    context.ReportDiagnostic(Diagnostic.Create(Rule, use.Value.Syntax.GetLocation(), use.Key.Name));
            }
        }
    }

    private static void Collect(IOperation operation, HashSet<ISymbol> written, List<IOperation> closures)
    {
        switch (operation)
        {
            case IAssignmentOperation assignment:
                AddWrittenTarget(assignment.Target, written);
                break;
            case IIncrementOrDecrementOperation increment:
                AddWrittenTarget(increment.Target, written);
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                AddWrittenTarget(argument.Value, written);
                break;
            case IForEachLoopOperation loop:
                AddWrittenTarget(loop.LoopControlVariable, written);
                break;
            case IAnonymousFunctionOperation or ILocalFunctionOperation:
                closures.Add(operation);
                break;
        }

        foreach (var child in operation.ChildOperations)
            Collect(child, written, closures);
    }

    private static void AddWrittenTarget(IOperation? target, HashSet<ISymbol> written)
    {
        switch (target)
        {
            case ILocalReferenceOperation { IsDeclaration: false } local:
                written.Add(local.Local);
                break;
            case IParameterReferenceOperation parameter:
                written.Add(parameter.Parameter);
                break;
            case ITupleOperation tuple:
                foreach (var element in tuple.Elements)
                    AddWrittenTarget(element, written);
                break;
            case IFieldReferenceOperation { Instance.Type.IsValueType: true } field:
                AddWrittenTarget(field.Instance, written);
                break;
            case IPropertyReferenceOperation { Instance.Type.IsValueType: true } property:
                AddWrittenTarget(property.Instance, written);
                break;
        }
    }

    private static void FindCapturedUses(
        IOperation operation,
        SyntaxNode closureSyntax,
        HashSet<ISymbol> written,
        Dictionary<ISymbol, IOperation> firstUses)
    {
        if (operation is INameOfOperation)
            return;

        ISymbol? variable = operation switch
        {
            ILocalReferenceOperation { IsImplicit: false } local => local.Local,
            IParameterReferenceOperation { IsImplicit: false } parameter => parameter.Parameter,
            _ => null
        };

        if (variable is not null
            && written.Contains(variable)
            && !IsDeclaredInside(variable, closureSyntax)
            && (!firstUses.TryGetValue(variable, out var previous)
                || operation.Syntax.SpanStart < previous.Syntax.SpanStart))
        {
            firstUses[variable] = operation;
        }

        foreach (var child in operation.ChildOperations)
            FindCapturedUses(child, closureSyntax, written, firstUses);
    }

    private static bool IsDeclaredInside(ISymbol variable, SyntaxNode closureSyntax)
    {
        foreach (var location in variable.Locations)
        {
            if (location.SourceTree == closureSyntax.SyntaxTree && closureSyntax.Span.Contains(location.SourceSpan))
                return true;
        }

        return false;
    }
}
