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
///     FP0201: a field inside the functional core that is neither <c>readonly</c> nor <c>const</c>, reported at
///     each variable declarator name. Static, struct, <c>volatile</c>, fixed-size buffer and <c>ref readonly</c>
///     ref fields count. Compiler-generated backing fields (including the one behind a field-like event) and
///     enum members (constants) are exempt.
///     <para>
///         A primary constructor parameter of a class or struct in core (not a record) is reported too, at its name in
///         the primary constructor's parameter list, once however often it is written, when a method, accessor,
///         finalizer, local function or lambda of the type writes it: assignment (simple, compound, <c>??=</c>),
///         <c>++</c>/<c>--</c>, a deconstruction target, or a <c>ref</c> or <c>out</c> argument. The compiler keeps
///         such a parameter in a field that cannot be <c>readonly</c>, so it is hidden mutable state just like a
///         declared field. A write in a field or property initializer or in the base constructor argument list (outside
///         a lambda) runs during construction. A write through a ref local also counts when it writes the parameter's
///         storage. A parameter that is only read and a record's positional parameters are exempt.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0201MutableFieldAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0201",
        "Do not declare mutable fields in the functional core",
        "Keep '{0}' immutable: declare a field readonly or const, leave a primary-constructor parameter unassigned, and return a new instance with the changed value");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        context.RegisterSymbolStartAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolStartAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsRecord
            || type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || !CoreScope.IsDeclaredInSource(type)
            || !CoreScope.IsInCore(type))
            return;

        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters.IsEmpty || !IsPrimaryConstructor(constructor, context.CancellationToken))
                continue;

            var writes = new PrimaryConstructorWrites(type, constructor);
            context.RegisterOperationBlockAction(writes.CollectBlock);
            context.RegisterSymbolEndAction(writes.Report);
            return;
        }
    }

    private static bool IsPrimaryConstructor(IMethodSymbol constructor, CancellationToken cancellationToken)
    {
        foreach (var reference in constructor.DeclaringSyntaxReferences)
            if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax)
                return true;

        return false;
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (context.Symbol is IFieldSymbol { IsConst: false, IsReadOnly: false } field)
            ReportIfInCore(context, field);
    }

    private static void ReportIfInCore(SymbolAnalysisContext context, ISymbol symbol)
    {
        if (!CoreScope.IsDeclaredInSource(symbol) || !CoreScope.IsInCore(symbol))
            return;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
            if (reference.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax declarator)
                context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.Identifier.GetLocation(), symbol.Name));
    }

    /// <summary>
    ///     Collects the primary constructor parameters that one type's members write (blocks are analyzed
    ///     concurrently) and reports each of them once when the type is complete.
    /// </summary>
    private sealed class PrimaryConstructorWrites(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        private readonly object _gate = new();
        private readonly HashSet<IParameterSymbol> _written = new(SymbolEqualityComparer.Default);

        public void CollectBlock(OperationBlockAnalysisContext context)
        {
            if (!SymbolEqualityComparer.Default.Equals(context.OwningSymbol.ContainingType, type))
                return;

            // In a method, accessor or finalizer every write counts. In the blocks that run only during
            // construction (field and property initializers, the base constructor argument list, which
            // belong to the primary constructor) only a write inside a lambda counts: the lambda keeps the
            // parameter alive and can change it later.
            var insideMember = context.OwningSymbol is IMethodSymbol method
                               && !SymbolEqualityComparer.Default.Equals(method, constructor);

            var written = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
            var aliases = new LocalAliases(context.OperationBlocks, context.CancellationToken);
            foreach (var block in context.OperationBlocks)
                CollectWrites(block, insideMember, aliases, written);

            if (written.Count == 0)
                return;

            lock (_gate)
            {
                _written.UnionWith(written);
            }
        }

        public void Report(SymbolAnalysisContext context)
        {
            lock (_gate)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (!_written.Contains(parameter))
                        continue;

                    foreach (var location in parameter.Locations)
                        if (location.IsInSource)
                            context.ReportDiagnostic(Diagnostic.Create(Rule, location, parameter.Name));
                }
            }
        }

        private void CollectWrites(IOperation root, bool insideMember, LocalAliases aliases, HashSet<IParameterSymbol> written)
        {
            var pending = new Stack<(IOperation Operation, bool Counts)>();
            pending.Push((root, insideMember));
            while (pending.Count > 0)
            {
                var (operation, counts) = pending.Pop();
                if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
                    counts = true;

                if (counts)
                    switch (operation)
                    {
                        case ISimpleAssignmentOperation { IsRef: true }:
                            break;
                        case IAssignmentOperation assignment:
                            AddWrittenTarget(assignment.Target, aliases, written);
                            break;
                        case IIncrementOrDecrementOperation increment:
                            AddWrittenTarget(increment.Target, aliases, written);
                            break;
                        case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                            AddWrittenTarget(argument.Value, aliases, written);
                            break;
                    }

                foreach (var child in operation.ChildOperations)
                    pending.Push((child, counts));
            }
        }

        private void AddWrittenTarget(IOperation? target, LocalAliases aliases, HashSet<IParameterSymbol> written,
            HashSet<ILocalSymbol>? visited = null)
        {
            switch (target)
            {
                case IParameterReferenceOperation reference
                    when SymbolEqualityComparer.Default.Equals(reference.Parameter.ContainingSymbol, constructor):
                    written.Add(reference.Parameter);
                    break;
                case ITupleOperation tuple:
                    foreach (var element in tuple.Elements)
                        AddWrittenTarget(element, aliases, written, visited);
                    break;
                case ILocalReferenceOperation { Local.IsRef: true } local:
                    visited ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
                    if (!visited.Add(local.Local))
                        break;

                    foreach (var source in aliases.Assignments(local.Local))
                        if (source.Reads.IsDefaultOrEmpty)
                            AddWrittenTarget(source.Value, aliases, written, visited);
                    break;
                case IConditionalOperation { IsRef: true } conditional:
                    AddWrittenTarget(conditional.WhenTrue, aliases, written, visited);
                    AddWrittenTarget(conditional.WhenFalse, aliases, written, visited);
                    break;
                case IFieldReferenceOperation { Field.RefKind: RefKind.None, Instance: { Type.IsValueType: true } instance }:
                    AddWrittenTarget(instance, aliases, written, visited);
                    break;
            }
        }
    }
}
