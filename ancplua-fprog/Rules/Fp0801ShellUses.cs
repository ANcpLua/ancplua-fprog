using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0801 for code that uses the imperative shell without writing the name of a forbidden type: a static member
///     imported with <c>using static</c>, an instance member on a shell object whose type is written nowhere in core
///     (an uncovered receiver), and a target-typed <c>new(...)</c> for a parameter declared outside core. One instance
///     serves one analyzer callback; it holds no state between callbacks.
///     <para>
///         A receiver is covered (nothing is reported for the member used on it) when FP0801 already reports a token
///         in it, or when the shell type enters the chain at <c>this</c>, <c>base</c> or a declaration in core with a
///         written type. FP0801 reports that written type itself when it is forbidden, so a member used on such a value
///         would be a second diagnostic for the same cause. A receiver is uncovered when the shell type enters at a member
///         declared outside core whose declared type names it (whatever stands to its left), or at an implicitly typed
///         variable whose source is uncovered. Whenever the analysis cannot tell, the receiver counts as covered: the rule
///         must not report correct code.
///     </para>
/// </summary>
internal sealed class Fp0801ShellUses(
    SemanticModel model,
    ISymbol? containingSymbol,
    CancellationToken cancellationToken)
{
    /// <summary>
    ///     How many implicitly typed variables the covered check follows back to their source. A longer chain of
    ///     <c>var</c> copies counts as covered; it keeps the check to one expression and a few declarations.
    /// </summary>
    private const int MaxDepth = 6;

    /// <summary>
    ///     The forbidden type whose static member <paramref name="name" /> uses through a <c>using static</c> directive,
    ///     or null. The operand of <c>nameof</c> is not a use. A static member found through the enclosing types or
    ///     their base types is not imported: the base list that brings it in names the type, and FP0801 reports
    ///     that name. A call bound at run time (a <c>dynamic</c> argument) counts only when every candidate is such
    ///     a member.
    /// </summary>
    public INamedTypeSymbol? ImportedStaticUse(SimpleNameSyntax name, SymbolInfo info)
    {
        if (!IsUnqualified(name))
            return null;

        INamedTypeSymbol? type = null;
        foreach (var member in BoundMembers(info))
        {
            if (!member.IsStatic
                || member is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IFieldSymbol
                    or IEventSymbol)
                || member.ContainingType is not { } containing
                || !Fp0801ImperativeShellAnalyzer.IsForbidden(containing)
                || IsReachableFromEnclosingTypes(containing))
                return null;

            type ??= containing;
        }

        return type is not null && !IsInNameof(name) ? type : null;
    }

    /// <summary>
    ///     The forbidden type behind the instance member <paramref name="name" /> when it is used on an uncovered
    ///     receiver, or null. The receiver's static type is forbidden, or the member (or a member it overrides) is
    ///     declared on a forbidden type.
    /// </summary>
    public INamedTypeSymbol? UncoveredInstanceUse(SimpleNameSyntax name, SymbolInfo info)
    {
        return ReceiverOf(name) is { } receiver
               && ForbiddenInstanceMemberType(receiver, info) is { } type
               && !IsInNameof(name)
               && !IsCovered(receiver, 0)
            ? type
            : null;
    }

    /// <summary>
    ///     The forbidden type that the target-typed <paramref name="creation" /> creates when it is an argument for a
    ///     parameter of a member outside core, or null. A member in core wrote the parameter's type there, where
    ///     FP0801 reports it. When the parameter's declared type uses a type parameter (<c>List&lt;T&gt;.Add</c>,
    ///     <c>Func&lt;T&gt;</c>), the type came from a type argument that the core wrote or inferred: not reported.
    /// </summary>
    public INamedTypeSymbol? UncoveredTargetTypedCreation(ImplicitObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is not ArgumentSyntax argument
            || model.GetTypeInfo(creation, cancellationToken).Type is not INamedTypeSymbol type
            || !Fp0801ImperativeShellAnalyzer.IsForbidden(type)
            || model.GetOperation(argument, cancellationToken) is not IArgumentOperation { Parameter: { } parameter }
            || CoreScope.IsInCore(parameter.ContainingSymbol)
            || MentionsTypeParameter(parameter.OriginalDefinition.Type))
            return null;

        return type;
    }

    /// <summary>
    ///     The forbidden type when <paramref name="info" /> is an instance member that belongs to the shell on
    ///     <paramref name="receiver" />, regardless of coverage. Extension members are not instance members of the
    ///     receiver's type and are left out.
    /// </summary>
    private INamedTypeSymbol? ForbiddenInstanceMemberType(ExpressionSyntax receiver, SymbolInfo info)
    {
        var member = BoundMembers(info).FirstOrDefault();
        if (member is null
            || member.IsStatic
            || member is not (IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol)
            || member is IMethodSymbol { MethodKind: MethodKind.ReducedExtension or MethodKind.LocalFunction }
            || member is IMethodSymbol { IsExtensionMethod: true }
            || member.ContainingType is null or { IsExtension: true })
            return null;

        if (model.GetTypeInfo(receiver, cancellationToken).Type is INamedTypeSymbol receiverType
            && Fp0801ImperativeShellAnalyzer.IsForbidden(receiverType))
            return receiverType;

        for (var current = member; current is not null; current = Overridden(current))
            if (current.ContainingType is { } declaring && Fp0801ImperativeShellAnalyzer.IsForbidden(declaring))
                return declaring;

        return null;
    }

    /// <summary>
    ///     True when <paramref name="expression" /> names the shell itself or its value comes from a declaration whose
    ///     type is written in core (see the class summary). With <paramref name="carried" />, the shell type reaches the
    ///     value only through a type argument or an element type, so a link whose type does not mention a shell type
    ///     cannot tell and counts as covered.
    /// </summary>
    private bool IsCovered(ExpressionSyntax expression, int depth, bool carried = false)
    {
        return depth > MaxDepth || ContainsReportedToken(expression) || IsEntryCovered(expression, depth, carried);
    }

    /// <summary>
    ///     True when <paramref name="expression" /> contains a token that FP0801 reports, or an instance member of a
    ///     forbidden type. Such a member is either reported itself (so the receiver around it is named) or its own
    ///     receiver is covered, and then this expression, which has the same root, is covered as well.
    /// </summary>
    private bool ContainsReportedToken(ExpressionSyntax expression)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
            switch (node)
            {
                case SimpleNameSyntax name when !name.IsVar:
                    var info = model.GetSymbolInfo(name, cancellationToken);
                    if ((info.Symbol is { } symbol
                         && Fp0801ImperativeShellAnalyzer.ReferencedType(symbol, name) is { } type
                         && Fp0801ImperativeShellAnalyzer.IsForbidden(type))
                        || ImportedStaticUse(name, info) is not null
                        || (ReceiverOf(name) is { } receiver &&
                            ForbiddenInstanceMemberType(receiver, info) is not null))
                        return true;

                    break;
                case ImplicitObjectCreationExpressionSyntax creation
                    when UncoveredTargetTypedCreation(creation) is not null:
                    return true;
            }

        return false;
    }

    /// <summary>
    ///     Finds where the shell type enters the chain that ends in <paramref name="expression" />, walking from the
    ///     receiver to the left, and decides there. A link that is <c>this</c> or <c>base</c>, or a symbol declared in
    ///     core (a parameter or local with a written type, a member of a core type), is covered: the core wrote the type.
    ///     A member declared outside core whose declared type names the shell type is uncovered, even when the chain to
    ///     its left starts at a typed core parameter (<c>repo.Connection.Open()</c>). An implicitly typed variable
    ///     follows its source. A member whose declared type uses a type parameter (<c>Lazy&lt;T&gt;.Value</c>, a list
    ///     indexer, <c>Enumerable.First</c> on a receiver) only passes on a type that came from further left, so the walk
    ///     goes on to its receiver; an array element goes on to the array. Anything else (a literal, an object
    ///     creation, a cast, a conditional, a static generic method, a member of an anonymous type) cannot tell and
    ///     counts as covered.
    /// </summary>
    private bool IsEntryCovered(ExpressionSyntax expression, int depth, bool carried)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    continue;
                case ConditionalAccessExpressionSyntax conditional:
                    current = conditional.WhenNotNull;
                    continue;
                case AwaitExpressionSyntax awaited:
                    current = awaited.Expression;
                    carried = true;
                    continue;
                case ThisExpressionSyntax or BaseExpressionSyntax:
                    return true;
            }

            if (carried && !MentionsShellType(model.GetTypeInfo(current, cancellationToken).Type))
                return true;

            var (symbol, next) = Link(current);
            switch (symbol)
            {
                case ILocalSymbol local:
                    return IsLocalCovered(local, depth);
                case IParameterSymbol parameter:
                    return IsParameterCovered(parameter, depth);
                case IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol
                    when CoreScope.IsInCore(symbol) || symbol.ContainingType is { IsAnonymousType: true }:
                    return true;
                case IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol
                    when !MentionsTypeParameter(DeclaredType(symbol)):
                    return false;
                case IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol or null when next is not null:
                    current = next;
                    carried = true;
                    continue;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    ///     The symbol that gives the value of one link of a chain, and the expression to its left that a type
    ///     argument of that value can come from (null when there is none: a static member, a member reached
    ///     unqualified, or a syntax that is not a link).
    /// </summary>
    private (ISymbol? Symbol, ExpressionSyntax? Next) Link(ExpressionSyntax link)
    {
        switch (link)
        {
            case SimpleNameSyntax name:
                return (model.GetSymbolInfo(name, cancellationToken).Symbol, null);
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                var member = model.GetSymbolInfo(access.Name, cancellationToken).Symbol;
                return (member,
                    member is { IsStatic: false } && !IsTypeOrNamespace(access.Expression) ? access.Expression : null);
            case MemberBindingExpressionSyntax binding:
                return (model.GetSymbolInfo(binding.Name, cancellationToken).Symbol, ConditionalReceiver(binding));
            case InvocationExpressionSyntax invocation:
                var method = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
                if (method is { MethodKind: MethodKind.DelegateInvoke })
                    return (method, invocation.Expression);

                return (method, method is { IsStatic: false } or { MethodKind: MethodKind.ReducedExtension }
                    ? Link(invocation.Expression).Next
                    : null);
            case ElementAccessExpressionSyntax elementAccess:
                return (model.GetSymbolInfo(elementAccess, cancellationToken).Symbol, elementAccess.Expression);
            case ElementBindingExpressionSyntax elementBinding:
                return (model.GetSymbolInfo(elementBinding, cancellationToken).Symbol,
                    ConditionalReceiver(elementBinding));
            default:
                return (null, null);
        }
    }

    private static ITypeSymbol DeclaredType(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.OriginalDefinition.Type,
            IPropertySymbol property => property.OriginalDefinition.Type,
            IEventSymbol @event => @event.OriginalDefinition.Type,
            _ => ((IMethodSymbol)member).ReducedFrom?.OriginalDefinition.ReturnType
                 ?? ((IMethodSymbol)member).OriginalDefinition.ReturnType
        };
    }

    /// <summary>True when <paramref name="type" /> is, derives from or has as a type argument a forbidden type.</summary>
    private static bool MentionsShellType(ITypeSymbol? type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return MentionsShellType(array.ElementType);
            case INamedTypeSymbol named:
                for (var current = named; current is not null; current = current.BaseType)
                    if (Fp0801ImperativeShellAnalyzer.IsForbidden(current))
                        return true;

                return named.TypeArguments.Any(MentionsShellType);
            default:
                return false;
        }
    }

    /// <summary>
    ///     A local with a written type is covered. A <c>var</c> local (also <c>using var</c>) is covered when its
    ///     initializer is, a <c>foreach (var ...)</c> variable when its collection is, and a pattern or deconstruction
    ///     variable when the value it comes from is.
    /// </summary>
    private bool IsLocalCovered(ILocalSymbol local, int depth)
    {
        switch (DeclaringSyntax(local))
        {
            case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } declarator:
                return !declaration.Type.IsVar
                       || declarator.Initializer is not { } initializer
                       || IsCovered(initializer.Value, depth + 1);
            case ForEachStatementSyntax forEach:
                return !forEach.Type.IsVar || IsCovered(forEach.Expression, depth + 1, true);
            case SingleVariableDesignationSyntax designation:
                return IsDesignationCovered(designation, depth);
            default:
                return true;
        }
    }

    private bool IsDesignationCovered(SingleVariableDesignationSyntax designation, int depth)
    {
        SyntaxNode node = designation;
        while (node.Parent is ParenthesizedVariableDesignationSyntax parenthesized)
            node = parenthesized;

        return node.Parent switch
        {
            DeclarationExpressionSyntax declaration => !declaration.Type.IsVar ||
                                                       IsDeclarationExpressionCovered(declaration, depth),
            VarPatternSyntax pattern => IsPatternCovered(pattern, depth),
            RecursivePatternSyntax pattern => pattern.Type is not null || IsPatternCovered(pattern, depth),
            _ => true
        };
    }

    /// <summary>
    ///     <c>out var x</c> follows the call it belongs to; <c>var (a, b) = e</c>, <c>(var a, var b) = e</c> and
    ///     <c>foreach (var (a, b) in e)</c> follow <c>e</c>.
    /// </summary>
    private bool IsDeclarationExpressionCovered(DeclarationExpressionSyntax declaration, int depth)
    {
        if (declaration.Parent is ArgumentSyntax
            {
                Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation }
            } argument)
            return IsCallCovered(invocation, argument, depth);

        SyntaxNode node = declaration;
        while (node.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple })
            node = tuple;

        return node.Parent switch
        {
            AssignmentExpressionSyntax assignment when assignment.Left == node =>
                IsCovered(assignment.Right, depth + 1),
            ForEachVariableStatementSyntax forEach when forEach.Variable == node
                => IsCovered(forEach.Expression, depth + 1, true),
            _ => true
        };
    }

    /// <summary>A designation directly in a top-level pattern follows the value that the pattern tests.</summary>
    private bool IsPatternCovered(PatternSyntax pattern, int depth)
    {
        SyntaxNode node = pattern;
        while (node.Parent is ParenthesizedPatternSyntax or BinaryPatternSyntax)
            node = node.Parent;

        return node.Parent switch
        {
            IsPatternExpressionSyntax isPattern => IsCovered(isPattern.Expression, depth + 1),
            CasePatternSwitchLabelSyntax { Parent.Parent: SwitchStatementSyntax switchStatement }
                => IsCovered(switchStatement.Expression, depth + 1),
            SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax switchExpression }
                => IsCovered(switchExpression.GoverningExpression, depth + 1),
            _ => true
        };
    }

    /// <summary>
    ///     A parameter with a written type is covered (also a primary-constructor or extension-block parameter, and the
    ///     implicit <c>value</c> of an accessor, whose type the property writes). A lambda parameter without a written
    ///     type follows the call that the lambda is an argument of.
    /// </summary>
    private bool IsParameterCovered(IParameterSymbol parameter, int depth)
    {
        if (DeclaringSyntax(parameter) is not ParameterSyntax { Type: null } syntax)
            return true;

        var lambda = syntax.Parent as LambdaExpressionSyntax ?? syntax.Parent?.Parent as LambdaExpressionSyntax;
        return lambda?.Parent is not ArgumentSyntax
               {
                   Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation }
               } argument
               || IsCallCovered(invocation, argument, depth);
    }

    /// <summary>
    ///     A variable that a call gives a value (<c>out var</c>, a lambda parameter) is covered when the call's receiver
    ///     is covered (for a call on a type or an unqualified call: the method) or another argument is covered.
    /// </summary>
    private bool IsCallCovered(InvocationExpressionSyntax invocation, ArgumentSyntax argument, int depth)
    {
        var receiver = invocation.Expression is MemberAccessExpressionSyntax access
                       && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                       && !IsThisOrBase(access.Expression)
                       && !IsTypeOrNamespace(access.Expression)
            ? access.Expression
            : invocation.Expression;

        return IsCovered(receiver, depth + 1)
               || invocation.ArgumentList.Arguments.Any(other =>
                   other != argument && IsCovered(other.Expression, depth + 1));
    }

    private SyntaxNode? DeclaringSyntax(ISymbol symbol)
    {
        var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();

        // Locals and parameters that a body uses are declared in the same tree; anything else cannot tell.
        return reference is not null && reference.SyntaxTree == model.SyntaxTree
            ? reference.GetSyntax(cancellationToken)
            : null;
    }

    private bool IsTypeOrNamespace(ExpressionSyntax expression)
    {
        return model.GetSymbolInfo(expression, cancellationToken).Symbol is INamespaceOrTypeSymbol;
    }

    /// <summary>True when a type that encloses the use, or one of its base types, is <paramref name="type" />.</summary>
    private bool IsReachableFromEnclosingTypes(INamedTypeSymbol type)
    {
        var target = type.OriginalDefinition;
        for (var enclosing = containingSymbol as INamedTypeSymbol ?? containingSymbol?.ContainingType;
             enclosing is not null;
             enclosing = enclosing.ContainingType)
        for (var current = enclosing; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
                return true;

        return false;
    }

    /// <summary>True when <paramref name="node" /> is inside the operand of the <c>nameof</c> operator.</summary>
    private bool IsInNameof(SyntaxNode node)
    {
        foreach (var invocation in node.Ancestors().OfType<InvocationExpressionSyntax>())
            if (invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } nameof
                && invocation.ArgumentList.Span.Contains(node.Span)
                && model.GetSymbolInfo(nameof, cancellationToken).Symbol is null)
                return true;

        return false;
    }

    /// <summary>The bound member, or every candidate of a call bound at run time.</summary>
    private static ImmutableArray<ISymbol> BoundMembers(SymbolInfo info)
    {
        return info.Symbol is { } symbol ? [symbol]
            : info.CandidateReason == CandidateReason.LateBound ? info.CandidateSymbols
            : [];
    }

    /// <summary>The receiver of <paramref name="name" /> when it is the member of <c>e.Name</c> or <c>e?.Name</c>.</summary>
    private static ExpressionSyntax? ReceiverOf(SimpleNameSyntax name)
    {
        return name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == name &&
                                                     access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                => access.Expression,
            MemberBindingExpressionSyntax binding => ConditionalReceiver(binding),
            _ => null
        };
    }

    /// <summary>The expression before the <c>?</c> that <paramref name="binding" /> (<c>.Name</c> or <c>[i]</c>) continues.</summary>
    private static ExpressionSyntax? ConditionalReceiver(ExpressionSyntax binding)
    {
        foreach (var conditional in binding.Ancestors().OfType<ConditionalAccessExpressionSyntax>())
            if (conditional.WhenNotNull.Span.Contains(binding.Span))
                return conditional.Expression;

        return null;
    }

    /// <summary>True when <paramref name="name" /> is written alone, not after a dot, <c>?.</c> or <c>::</c>.</summary>
    private static bool IsUnqualified(SimpleNameSyntax name)
    {
        return name.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Name != name,
            QualifiedNameSyntax qualified => qualified.Right != name,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name != name,
            MemberBindingExpressionSyntax or NameColonSyntax or NameEqualsSyntax => false,
            _ => true
        };
    }

    private static bool IsThisOrBase(ExpressionSyntax expression)
    {
        return expression is ThisExpressionSyntax or BaseExpressionSyntax;
    }

    private static ISymbol? Overridden(ISymbol member)
    {
        return member switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null
        };
    }

    private static bool MentionsTypeParameter(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => MentionsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => MentionsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named => named.TypeArguments.Any(MentionsTypeParameter)
                                      || (named.ContainingType is { } outer && MentionsTypeParameter(outer)),
            _ => false
        };
    }
}