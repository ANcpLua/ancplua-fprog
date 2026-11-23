using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Shared;

/// <summary>
///     What a local variable stands for: <see cref="Source" /> itself, or, for a <c>foreach</c> iteration
///     variable, an element of the collection <see cref="Source" />.
/// </summary>
internal sealed class LocalAlias(IOperation source, ITypeSymbol? elementType, bool boxes)
{
    /// <summary>The initializer, the tested expression of a pattern, or the collection of a <c>foreach</c>.</summary>
    public IOperation Source { get; } = source;

    /// <summary>For a <c>foreach</c> variable, the type of the elements read from the collection; otherwise null.</summary>
    public ITypeSymbol? ElementType { get; } = elementType;

    /// <summary>
    ///     True when the local holds a box of a struct value that has no conversion operation of its own: a
    ///     reference-typed pattern variable over a struct value, or a reference-typed <c>foreach</c> variable over
    ///     struct elements. The local then refers to a fresh copy, not to the value it was taken from.
    /// </summary>
    public bool Boxes { get; } = boxes;
}

/// <summary>
///     The locals of one operation block (a member body with its lambdas and local functions, or an initializer)
///     that are another name for an expression: a local declared with an initializer, a <c>foreach</c> iteration
///     variable, or a pattern variable declared by the top-level pattern (also through <c>and</c>) of an
///     <c>is</c> expression, a <c>case</c> label or a switch-expression arm. A local that is written anywhere in
///     the block after its declaration (assignment, compound assignment, <c>??=</c>, <c>++</c>/<c>--</c>,
///     deconstruction, <c>ref</c>/<c>out</c>/<c>in</c> argument, <c>ref</c> alias, address taken) or that is a
///     <c>ref</c> local is no alias: it may stand for something else by the time it is used. The table is built
///     lazily, in one walk over the block, the first time a caller asks; it is read-only afterwards and safe to
///     share between threads.
/// </summary>
internal sealed class LocalAliases
{
    private readonly Lazy<(Dictionary<ILocalSymbol, LocalAlias> Aliases, HashSet<ILocalSymbol> Written)> _table;

    public LocalAliases(ImmutableArray<IOperation> blocks, CancellationToken cancellationToken)
    {
        _table = new Lazy<(Dictionary<ILocalSymbol, LocalAlias> Aliases, HashSet<ILocalSymbol> Written)>(
            () => Build(blocks, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    ///     The expression <paramref name="local" /> stands for, when it is declared in this block, never written
    ///     again and its type may be a reference (a local of a value type is a copy and never an alias).
    /// </summary>
    public bool TryGet(ILocalSymbol local, out LocalAlias alias)
    {
        alias = null!;
        if (local.RefKind != RefKind.None || !MayBeReference(local.Type))
            return false;

        var (aliases, written) = _table.Value;
        if (written.Contains(local) || !aliases.TryGetValue(local, out var found))
            return false;

        alias = found;
        return true;
    }

    private static (Dictionary<ILocalSymbol, LocalAlias>, HashSet<ILocalSymbol>) Build(
        ImmutableArray<IOperation> blocks,
        CancellationToken cancellationToken)
    {
        var aliases = new Dictionary<ILocalSymbol, LocalAlias>(SymbolEqualityComparer.Default);
        var written = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        foreach (var block in blocks)
        foreach (var operation in block.DescendantsAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (operation)
            {
                case ILocalReferenceOperation reference when IsWritten(reference):
                    written.Add(reference.Local);
                    break;
                case IVariableDeclaratorOperation declarator
                    when declarator.GetVariableInitializer() is { Value: var value }:
                    aliases[declarator.Symbol] = new LocalAlias(value, null, false);
                    break;
                case IForEachLoopOperation { LoopControlVariable: IVariableDeclaratorOperation variable } loop
                    when ElementType(loop) is { } elementType:
                    // Converting a struct element to a reference-typed variable boxes a copy of it.
                    aliases[variable.Symbol] = new LocalAlias(
                        loop.Collection,
                        elementType,
                        elementType.IsValueType && MayBeReference(variable.Symbol.Type));
                    break;
                case IIsPatternOperation isPattern:
                    AddPatternVariables(aliases, isPattern.Pattern, isPattern.Value);
                    break;
                case IPatternCaseClauseOperation { Parent.Parent: ISwitchOperation switchStatement } clause:
                    AddPatternVariables(aliases, clause.Pattern, switchStatement.Value);
                    break;
                case ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation switchExpression } arm:
                    AddPatternVariables(aliases, arm.Pattern, switchExpression.Value);
                    break;
            }
        }

        return (aliases, written);
    }

    /// <summary>Records the variables that the top-level pattern declares for the value it tests.</summary>
    private static void AddPatternVariables(
        Dictionary<ILocalSymbol, LocalAlias> aliases,
        IPatternOperation pattern,
        IOperation tested)
    {
        switch (pattern)
        {
            // Parentheses around a pattern have no operation of their own.
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both:
                AddPatternVariables(aliases, both.LeftPattern, tested);
                AddPatternVariables(aliases, both.RightPattern, tested);
                break;
            case IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol local }:
                Add(local);
                break;
            case IRecursivePatternOperation { DeclaredSymbol: ILocalSymbol local }:
                Add(local);
                break;
            case IListPatternOperation { DeclaredSymbol: ILocalSymbol local }:
                Add(local);
                break;
        }

        void Add(ILocalSymbol local)
        {
            aliases[local] = new LocalAlias(
                tested,
                null,
                // A reference-typed variable over a struct value holds a box: a copy.
                tested.Type is { IsValueType: true } && MayBeReference(local.Type));
        }
    }

    /// <summary>The type of the elements a <c>foreach</c> reads, or null when the compiler could not tell.</summary>
    private static ITypeSymbol? ElementType(IForEachLoopOperation loop)
    {
        return loop is { Syntax: CommonForEachStatementSyntax syntax, SemanticModel: { } model }
            ? model.GetForEachStatementInfo(syntax).ElementType
            : null;
    }

    /// <summary>
    ///     True when <paramref name="reference" /> may give its local another value or let other code do so: it is
    ///     the target of an assignment of any kind (also inside a deconstruction), of <c>++</c>/<c>--</c>, a
    ///     <c>ref</c>, <c>out</c> or <c>in</c> argument, the operand of <c>ref</c> (a ref local, ref return or ref
    ///     conditional that aliases it) or of <c>&amp;</c>. Arguments are judged by their keyword, not by the
    ///     parameter, so that dynamic calls count too; counting an <c>in</c> argument only loses an alias.
    /// </summary>
    private static bool IsWritten(ILocalReferenceOperation reference)
    {
        IOperation current = reference;
        while (current.Parent is ITupleOperation)
            current = current.Parent;

        switch (current.Parent)
        {
            case IAssignmentOperation assignment when assignment.Target == current:
            case IIncrementOrDecrementOperation increment when increment.Target == current:
            case IForEachLoopOperation loop when loop.LoopControlVariable == current:
            case IAddressOfOperation:
                return true;
        }

        var syntax = reference.Syntax;
        while (syntax.Parent is ParenthesizedExpressionSyntax parenthesized)
            syntax = parenthesized;

        return syntax.Parent is RefExpressionSyntax
               || (syntax.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None));
    }

    /// <summary>
    ///     True when a value of <paramref name="type" /> may be a reference to an object: classes, interfaces,
    ///     arrays, delegates, <c>dynamic</c> and type parameters not known to be value types.
    /// </summary>
    public static bool MayBeReference(ITypeSymbol? type)
    {
        return type is { IsValueType: false, TypeKind: not TypeKind.Pointer and not TypeKind.FunctionPointer };
    }
}