using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0701: inside the functional core, a lambda, anonymous method or local function captures a local
/// or parameter that is written anywhere other than by its declaration: assignment, compound or
/// coalescing assignment, <c>++</c>/<c>--</c>, deconstruction, a <c>ref</c> or <c>out</c> argument, binding
/// a writable <c>ref</c> alias to it, or a write to a field, inline-array element or mutating property of a
/// value-type variable. Locals and ordinary parameters are written only inside their member; a primary
/// constructor parameter can be written by any member of its type. Reported once per variable and
/// closure, at the first reference to the variable inside the closure; a reference that is the first one
/// of several nested closures is reported once.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0701MutableCaptureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0701",
        "Do not capture a variable that is reassigned",
        "The closure captures '{0}', which is assigned after its declaration; capture a new local that is never reassigned instead (for example 'var {0}Value = {0};' after the last write), or pass the value to the closure as a parameter");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeMember);
        context.RegisterSymbolStartAction(AnalyzeType, SymbolKind.NamedType);
    }

    /// <summary>Locals and parameters other than primary constructor parameters: all writes are in this member.</summary>
    private static void AnalyzeMember(OperationBlockAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.OwningSymbol))
            return;

        var written = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var closures = new List<IOperation>();
        foreach (var block in context.OperationBlocks)
            CollectWritesAndClosures(block, written, closures);

        written.RemoveWhere(variable => IsPrimaryConstructorParameter(variable, context.CancellationToken));
        if (written.Count == 0 || closures.Count == 0)
            return;

        var reported = new HashSet<SyntaxNode>();
        foreach (var closure in closures)
        {
            foreach (var use in FindFirstUses(closure, written.Contains))
            {
                if (reported.Add(use.Value))
                    context.ReportDiagnostic(Diagnostic.Create(Rule, use.Value.GetLocation(), use.Value.ToString()));
            }
        }
    }

    /// <summary>Primary constructor parameters are in scope in the whole type, so every member can write them.</summary>
    private static void AnalyzeType(SymbolStartAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!CoreScope.IsInCore(type))
            return;

        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters.IsEmpty || !IsPrimaryConstructor(constructor, context.CancellationToken))
                continue;

            var captures = new PrimaryConstructorCaptures(type, constructor);
            context.RegisterOperationBlockAction(captures.CollectBlock);
            context.RegisterSymbolEndAction(captures.Report);
            return;
        }
    }

    private static bool IsPrimaryConstructorParameter(ISymbol variable, CancellationToken cancellationToken)
        => variable is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } constructor }
           && IsPrimaryConstructor(constructor, cancellationToken);

    private static bool IsPrimaryConstructor(IMethodSymbol constructor, CancellationToken cancellationToken)
    {
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax)
                return true;
        }

        return false;
    }

    private static void CollectWritesAndClosures(IOperation root, HashSet<ISymbol> written, List<IOperation> closures)
    {
        var pending = new Stack<IOperation>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            switch (operation)
            {
                case ISimpleAssignmentOperation { IsRef: true } refAssignment:
                    AddWrittenTarget(refAssignment.Target, written);
                    if (IsWritableReference(refAssignment.Target))
                        AddWrittenTarget(refAssignment.Value, written);
                    break;
                case IAssignmentOperation assignment:
                    AddWrittenTarget(assignment.Target, written);
                    break;
                case IIncrementOrDecrementOperation increment:
                    AddWrittenTarget(increment.Target, written);
                    break;
                case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                    AddWrittenTarget(argument.Value, written);
                    break;
                case IVariableDeclaratorOperation { Symbol.RefKind: RefKind.Ref } refLocal:
                    AddWrittenTarget(refLocal.GetVariableInitializer()?.Value, written);
                    break;
                case IAnonymousFunctionOperation or ILocalFunctionOperation:
                    closures.Add(operation);
                    break;
            }

            foreach (var child in operation.ChildOperations)
                pending.Push(child);
        }
    }

    /// <summary>True when the target of a <c>ref</c> reassignment allows writes through it (not <c>ref readonly</c>).</summary>
    private static bool IsWritableReference(IOperation target) => target switch
    {
        ILocalReferenceOperation local => local.Local.RefKind == RefKind.Ref,
        IParameterReferenceOperation parameter => parameter.Parameter.RefKind is RefKind.Ref or RefKind.Out,
        IFieldReferenceOperation field => field.Field.RefKind == RefKind.Ref,
        _ => false
    };

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
            case IConditionalOperation { IsRef: true } conditional:
                AddWrittenTarget(conditional.WhenTrue, written);
                AddWrittenTarget(conditional.WhenFalse, written);
                break;
            case IFieldReferenceOperation { Instance.Type.IsValueType: true } field:
                AddWrittenTarget(field.Instance, written);
                break;
            case IPropertyReferenceOperation { Instance.Type.IsValueType: true } property
                when MutatesInstance(property.Property):
                AddWrittenTarget(property.Instance, written);
                break;
            case IInlineArrayAccessOperation inlineArray:
                AddWrittenTarget(inlineArray.Instance, written);
                break;
        }
    }

    /// <summary>
    /// A write through a <c>readonly</c> accessor (every accessor of a <c>readonly struct</c>) cannot change the
    /// variable it is called on, for example the indexer setter of <c>ArraySegment&lt;T&gt;</c>.
    /// </summary>
    private static bool MutatesInstance(IPropertySymbol property)
        => (property.ReturnsByRef ? property.GetMethod : property.SetMethod) is { IsReadOnly: false };

    /// <summary>The first reference inside <paramref name="closure"/> to each captured candidate variable.</summary>
    private static Dictionary<ISymbol, SyntaxNode> FindFirstUses(IOperation closure, Func<ISymbol, bool> isCandidate)
    {
        var firstUses = new Dictionary<ISymbol, SyntaxNode>(SymbolEqualityComparer.Default);
        var pending = new Stack<IOperation>();
        pending.Push(closure);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            if (operation is INameOfOperation)
                continue;

            ISymbol? variable = operation switch
            {
                ILocalReferenceOperation { IsImplicit: false } local => local.Local,
                IParameterReferenceOperation { IsImplicit: false } parameter => parameter.Parameter,
                _ => null
            };

            if (variable is not null
                && isCandidate(variable)
                && !IsDeclaredInside(variable, closure.Syntax)
                && (!firstUses.TryGetValue(variable, out var previous)
                    || operation.Syntax.SpanStart < previous.SpanStart))
            {
                firstUses[variable] = operation.Syntax;
            }

            foreach (var child in operation.ChildOperations)
                pending.Push(child);
        }

        return firstUses;
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

    /// <summary>
    /// Writes to and captures of one type's primary constructor parameters, gathered over all its members
    /// (concurrently) and reported when the type is complete.
    /// </summary>
    private sealed class PrimaryConstructorCaptures(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        private readonly object _gate = new();
        private readonly HashSet<ISymbol> _written = new(SymbolEqualityComparer.Default);
        private readonly List<KeyValuePair<ISymbol, SyntaxNode>> _uses = [];

        public void CollectBlock(OperationBlockAnalysisContext context)
        {
            if (!SymbolEqualityComparer.Default.Equals(context.OwningSymbol.ContainingType, type))
                return;

            var written = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var closures = new List<IOperation>();
            foreach (var block in context.OperationBlocks)
                CollectWritesAndClosures(block, written, closures);

            var uses = new List<KeyValuePair<ISymbol, SyntaxNode>>();
            foreach (var closure in closures)
                uses.AddRange(FindFirstUses(closure, IsOwnParameter));

            lock (_gate)
            {
                foreach (var variable in written)
                {
                    if (IsOwnParameter(variable))
                        _written.Add(variable);
                }

                _uses.AddRange(uses);
            }
        }

        public void Report(SymbolAnalysisContext context)
        {
            var reported = new HashSet<SyntaxNode>();
            lock (_gate)
            {
                foreach (var use in _uses)
                {
                    if (_written.Contains(use.Key) && reported.Add(use.Value))
                        context.ReportDiagnostic(Diagnostic.Create(Rule, use.Value.GetLocation(), use.Value.ToString()));
                }
            }
        }

        private bool IsOwnParameter(ISymbol variable)
            => variable is IParameterSymbol parameter
               && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, constructor);
    }
}
