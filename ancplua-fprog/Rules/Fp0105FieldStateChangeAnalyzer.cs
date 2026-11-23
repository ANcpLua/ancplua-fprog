using System;
using System.Collections.Immutable;
using System.Linq;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>
///     FP0105: inside the functional core, a change to an object that is held in a field. A <c>readonly</c>
///     field keeps the same object for ever, but the object can still change, and then the same call gives a
///     different answer the next time (a memo, a cache, a list that grows) and is no longer safe across threads.
///     <para>
///         The changed object is reached from a field when the chain from the write goes back to a field (an
///         instance field reached from <c>this</c>, written or implicit, or a static field of any type) or to an
///         auto-property declared in source without a <c>set</c> accessor (it is a field in effect), only through
///         fields, properties, indexers, array elements, casts, <c>as</c>, <c>!</c> and <c>?.</c>, and some step of
///         that chain (the field included) is a reference or returns by reference, so the write lands in a shared
///         object and not in the field's own storage. Reported:
///         an assignment, compound assignment, <c>??=</c>, <c>++</c>/<c>--</c>, deconstruction target or argument
///         bound to a <c>ref</c>/<c>out</c> parameter whose target is a field, property, indexer or element of such
///         an object (<c>_totals[0]++</c>, <c>Cache[sku] = price</c>, <c>_holder.Value = 1</c>), at the target as
///         written; a call to a mutating instance member of a mutable BCL collection or <c>StringBuilder</c> on such
///         an object (<c>_items.Add(x)</c>), and a static <c>System.Array</c> helper that writes into such an array
///         (<c>Array.Sort(_a)</c>, <c>Array.Copy(source, _a, n)</c>), at the method name. A memo such as
///         <c>Memo.GetOrAdd(n, k => k * k)</c> on a static <c>ConcurrentDictionary</c> is reported too: whether a
///         cache that only ever adds what a pure function returns is acceptable is the owner's decision, and the
///         rule does not try to tell such a cache from one that changes answers.
///     </para>
///     <para>
///         A mutating member is decided by the bound method, never by its name alone: its containing type must be
///         <c>System.Array</c>, <c>System.Text.StringBuilder</c>, or a type in <c>System.Collections</c> or below
///         outside <c>.Immutable</c> and <c>.Frozen</c>, where only the nested <c>Builder</c> types count, and its
///         name must be in <see cref="MutatingMembers" />. So <c>items.Append(x)</c>, <c>values.Reverse()</c> and
///         <c>set.Union(other)</c>, which bind to <c>System.Linq.Enumerable</c>, and <c>Add</c> on an immutable
///         collection, which returns a new one, are not reported.
///     </para>
///     <para>
///         Not reported: a chain whose root is a local or a parameter (locals are the function's own business,
///         writes through parameters are FP0104's); a chain through a method call, whose result may be a fresh
///         object; writes in an instance constructor or instance initializer of the type that declares the instance
///         field or of a type derived from it (the object is being built), and in a static constructor or static
///         initializer of the type that declares the static field; assigning the field itself (FP0201 and FP0102
///         cover fields that are not <c>readonly</c>); writes into a struct field's own storage, also through
///         <c>this</c> in a struct; reads. Known gaps: a field reached through a local alias, a property with a
///         <c>set</c> accessor, captured primary-constructor parameters, extension methods (such as
///         <c>CollectionExtensions.TryAdd</c>) and span helpers, and event subscriptions.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0105FieldStateChangeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0105",
        "Do not change state held in a field of the functional core",
        "Return a new value instead of changing the state held in '{0}'");

    /// <summary>
    ///     The instance members of the mutable collections and of <c>StringBuilder</c> that change the object,
    ///     checked against the .NET 11 reference assemblies. <c>Reverse</c> counts only when it returns nothing:
    ///     <c>SortedSet&lt;T&gt;.Reverse()</c> returns a reversed view and changes nothing.
    /// </summary>
    private static readonly ImmutableHashSet<string> MutatingMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        // Lists, sets, dictionaries, ObjectModel collections, ArrayList, Hashtable and the immutable builders.
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll", "RemoveRange",
        "RemoveWhere", "Clear", "Sort", "Reverse", "SetRange", "Move", "SetAt", "SetValueAtIndex", "TryAdd",
        "UnionWith", "IntersectWith", "ExceptWith", "SymmetricExceptWith", "TrimExcess", "TrimToSize",
        "EnsureCapacity", "Replace", "MoveToImmutable", "DrainToImmutable",
        // Stacks, queues, linked lists and the concurrent collections.
        "Push", "PushRange", "Pop", "TryPop", "TryPopRange", "Enqueue", "EnqueueRange", "EnqueueDequeue",
        "Dequeue", "DequeueEnqueue", "TryDequeue", "AddFirst", "AddLast", "AddBefore", "AddAfter",
        "RemoveFirst", "RemoveLast", "TryRemove", "TryTake", "Take", "TryUpdate", "AddOrUpdate", "GetOrAdd",
        "CompleteAdding",
        // BitArray and Array.SetValue.
        "Set", "SetAll", "And", "Or", "Xor", "Not", "LeftShift", "RightShift", "SetValue",
        // StringBuilder (Insert, Remove, Replace, Clear and EnsureCapacity are listed above).
        "Append", "AppendLine", "AppendFormat", "AppendJoin");

    /// <summary>The static <c>System.Array</c> helpers that write into an array argument.</summary>
    private static readonly ImmutableHashSet<string> ArrayWriters = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Sort", "Reverse", "Clear", "Fill", "Copy", "ConstrainedCopy");

    /// <summary>The parameters of <see cref="ArrayWriters" /> whose array is written.</summary>
    private static readonly ImmutableHashSet<string> WrittenArrayParameters = ImmutableHashSet.Create(
        StringComparer.Ordinal, "array", "keys", "items", "destinationArray");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Operation actions, unlike syntax node actions, run once for code in a C# 15 union body (Roslyn
        // 5.9.0), so no repeated pass has to be filtered out here.
        context.RegisterOperationAction(
            Analyze,
            OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment,
            OperationKind.Increment,
            OperationKind.Decrement,
            OperationKind.Argument,
            OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        switch (context.Operation)
        {
            case IDeconstructionAssignmentOperation deconstruction:
                CheckTarget(context, deconstruction.Target);
                break;
            case ISimpleAssignmentOperation { IsRef: true }:
                // `x.F = ref y` rebinds the ref field F; it writes nothing through the storage F referred to.
                break;
            case IAssignmentOperation assignment:
                CheckTarget(context, assignment.Target);
                break;
            case IIncrementOrDecrementOperation increment:
                CheckTarget(context, increment.Target);
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                // Decided by the parameter: `ref` passed to a `ref readonly` parameter and `in` are reads.
                CheckTarget(context, argument.Value);
                break;
            case IInvocationOperation invocation:
                CheckCall(context, invocation);
                break;
        }
    }

    private static void CheckTarget(OperationAnalysisContext context, IOperation target)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
                CheckTarget(context, element);

            return;
        }

        // Only a member or element of the object counts: a target that is the field itself has no receiver
        // on the path (its receiver is `this` or nothing).
        if (target is not (IFieldReferenceOperation or IPropertyReferenceOperation
            or IArrayElementReferenceOperation or IImplicitIndexerReferenceOperation))
            return;

        var start = target.Syntax.SpanStart;
        if (FieldRoot(Receiver(target), ReturnsByRef(target), ref start) is not { } root
            || IsConstruction(root, context.ContainingSymbol))
            return;

        // The target expression as written: `(_a[0]) = 1` is reported at `(_a[0])`.
        var syntax = target.Syntax;
        while (syntax.Parent is ParenthesizedExpressionSyntax parenthesized)
            syntax = parenthesized;

        var span = TextSpan.FromBounds(Math.Min(start, syntax.SpanStart), syntax.Span.End);
        context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(syntax.SyntaxTree, span), root.Name));
    }

    private static void CheckCall(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        ISymbol? root = null;
        if (!method.IsStatic)
        {
            if (IsMutatingMember(method))
                root = Root(invocation.Instance);
        }
        else if (method.ContainingType?.SpecialType == SpecialType.System_Array && ArrayWriters.Contains(method.Name))
        {
            foreach (var argument in invocation.Arguments)
                if (argument.Parameter is { } parameter
                    && WrittenArrayParameters.Contains(parameter.Name)
                    && Root(argument.Value) is { } written)
                {
                    root = written;
                    break;
                }
        }

        if (root is not null && NameToken(invocation.Syntax) is { } name)
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), root.Name));

        ISymbol? Root(IOperation? changed)
        {
            var unused = 0;
            return FieldRoot(changed, false, ref unused) is { } found
                   && !IsConstruction(found, context.ContainingSymbol)
                ? found
                : null;
        }
    }

    /// <summary>The name of the called method as written, or null for an implicit call.</summary>
    private static SyntaxToken? NameToken(SyntaxNode syntax)
    {
        return syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name.Identifier,
            InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } => binding.Name.Identifier,
            InvocationExpressionSyntax { Expression: SimpleNameSyntax simple } => simple.Identifier,
            _ => null
        };
    }

    /// <summary>
    ///     True when <paramref name="method" /> changes the collection or builder it is called on, decided by the
    ///     type that declares the bound method and by <see cref="MutatingMembers" />.
    /// </summary>
    private static bool IsMutatingMember(IMethodSymbol method)
    {
        return MutatingMembers.Contains(method.Name)
               && (method.Name != "Reverse" || method.ReturnsVoid)
               && method.ContainingType is { } type
               && IsMutableCollectionType(type);
    }

    private static bool IsMutableCollectionType(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Array)
            return true;

        if (type is { Name: "StringBuilder", ContainingType: null }
            && IsNamespace(type.ContainingNamespace, "System", "Text"))
            return true;

        var outermost = type;
        while (outermost.ContainingType is { } containing)
            outermost = containing;

        var ns = outermost.ContainingNamespace;
        if (IsNamespaceOrBelow(ns, "System", "Collections", "Immutable")
            || IsNamespaceOrBelow(ns, "System", "Collections", "Frozen"))
            // `ImmutableArray<T>.Builder` and its kind are mutable; everything else there returns new values.
            return type is { Name: "Builder", ContainingType: not null };

        return IsNamespaceOrBelow(ns, "System", "Collections");
    }

    /// <summary>True when <paramref name="ns" /> is the namespace <paramref name="names" /> (outermost first).</summary>
    private static bool IsNamespace(INamespaceSymbol? ns, params string[] names)
    {
        for (var index = names.Length - 1; index >= 0; index--)
        {
            if (ns is null || ns.IsGlobalNamespace || ns.Name != names[index])
                return false;
            ns = ns.ContainingNamespace;
        }

        return ns is { IsGlobalNamespace: true };
    }

    /// <summary>True when <paramref name="ns" /> is the namespace <paramref name="names" /> or nested in it.</summary>
    private static bool IsNamespaceOrBelow(INamespaceSymbol? ns, params string[] names)
    {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            if (IsNamespace(current, names))
                return true;

        return false;
    }

    /// <summary>
    ///     The field, or auto-property without a <c>set</c>, that the object changed at <paramref name="receiver" />
    ///     is reached from, or null. The walk takes only access-path steps; <paramref name="shared" /> becomes true
    ///     once a step reads a reference or a member that returns by reference, and a root is only returned when
    ///     it is, because otherwise the write lands in the field's own storage (a struct). A box without a shared
    ///     step above it is a copy, so the walk stops there. <paramref name="start" /> moves to the start of a
    ///     null-conditional access on the way.
    /// </summary>
    private static ISymbol? FieldRoot(IOperation? receiver, bool shared, ref int start)
    {
        while (receiver is not null)
        {
            if (!IsAccessPathStep(receiver))
                return null;

            if (receiver is IConversionOperation boxing && IsBoxing(boxing))
            {
                if (!shared)
                    return null;

                receiver = boxing.Operand;
                continue;
            }

            shared |= ReturnsByRef(receiver) || MayBeReference(receiver.Type);
            switch (receiver)
            {
                case IFieldReferenceOperation field when IsRoot(field.Field, field.Instance):
                    return shared ? field.Field : null;
                case IPropertyReferenceOperation property
                    when IsFieldLikeProperty(property.Property) && IsRoot(property.Property, property.Instance):
                    return shared ? property.Property : null;
                case IConditionalAccessInstanceOperation instance:
                    var access = EnclosingAccess(instance);
                    if (access is null)
                        return null;
                    start = access.Syntax.SpanStart;
                    receiver = access.Operation;
                    break;
                case IConversionOperation conversion:
                    receiver = conversion.Operand;
                    break;
                default:
                    receiver = Receiver(receiver);
                    break;
            }
        }

        return null;
    }

    /// <summary>A static member, or an instance member of the object the code runs on (<c>this</c>, <c>base</c>).</summary>
    private static bool IsRoot(ISymbol member, IOperation? instance)
    {
        return member.IsStatic
               || instance is IInstanceReferenceOperation
               {
                   ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
               };
    }

    /// <summary>
    ///     True for a property declared in source with a backing field (an auto-property, or one that uses
    ///     <c>field</c>) and no <c>set</c> accessor (<c>init</c> is allowed): it holds one object like a field.
    /// </summary>
    private static bool IsFieldLikeProperty(IPropertySymbol property)
    {
        if (property.IsIndexer
            || property.SetMethod is { IsInitOnly: false }
            || !CoreScope.IsDeclaredInSource(property))
            return false;

        foreach (var member in property.ContainingType.GetMembers("<" + property.Name + ">k__BackingField"))
            if (member is IFieldSymbol field && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property))
                return true;

        return false;
    }

    /// <summary>
    ///     True when the code runs while the object or type that holds <paramref name="root" /> is being built:
    ///     for an instance member, an instance constructor or instance initializer of its type or of a type derived
    ///     from it; for a static member, a static constructor or static initializer of its type. Lambdas and local
    ///     functions count as part of the member they are written in.
    /// </summary>
    private static bool IsConstruction(ISymbol root, ISymbol? containing)
    {
        var owner = containing;
        while (owner is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
            owner = owner.ContainingSymbol;

        if (owner is null || root.ContainingType is not { } declaring)
            return false;

        var building = owner switch
        {
            IMethodSymbol { MethodKind: MethodKind.StaticConstructor } => root.IsStatic,
            IMethodSymbol { MethodKind: MethodKind.Constructor } => !root.IsStatic,
            IFieldSymbol or IPropertySymbol => owner.IsStatic == root.IsStatic,
            _ => false
        };
        if (!building)
            return false;

        if (root.IsStatic)
            return SymbolEqualityComparer.Default.Equals(owner.ContainingType, declaring);

        for (var type = owner.ContainingType; type is not null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, declaring.OriginalDefinition))
                return true;

        return false;
    }

    /// <summary>
    ///     True when <paramref name="operation" /> may be a step of an access path: a field, property, indexer or
    ///     array element read, a cast or <c>as</c> without a user-defined operator, or the receiver of a
    ///     <c>?.</c>. A method call, a range (<c>values[1..]</c> copies) or a dynamic access ends the path.
    /// </summary>
    private static bool IsAccessPathStep(IOperation operation)
    {
        return operation switch
        {
            IFieldReferenceOperation or IConditionalAccessInstanceOperation => true,
            IPropertyReferenceOperation property => !HasRange(property.Arguments),
            IArrayElementReferenceOperation element => !element.Indices.Any(IsRange),
            IImplicitIndexerReferenceOperation indexer => !IsRange(indexer.Argument),
            IConversionOperation conversion => conversion.OperatorMethod is null,
            _ => false
        };
    }

    private static bool HasRange(ImmutableArray<IArgumentOperation> arguments)
    {
        foreach (var argument in arguments)
            if (IsRange(argument.Value))
                return true;

        return false;
    }

    private static bool IsRange(IOperation index)
    {
        return index.Type is { Name: "Range" } type && IsNamespace(type.ContainingNamespace, "System");
    }

    /// <summary>The value a field, property, element or indexer is read from, or null.</summary>
    private static IOperation? Receiver(IOperation operation)
    {
        return operation switch
        {
            IMemberReferenceOperation member => member.Instance,
            IArrayElementReferenceOperation element => element.ArrayReference,
            IImplicitIndexerReferenceOperation indexer => indexer.Instance,
            _ => null
        };
    }

    /// <summary>True when the operation denotes storage elsewhere: a member that returns by reference.</summary>
    private static bool ReturnsByRef(IOperation operation)
    {
        return operation switch
        {
            IFieldReferenceOperation field => field.Field.RefKind != RefKind.None,
            IPropertyReferenceOperation property => property.Property.RefKind != RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property } => property.RefKind !=
                RefKind.None,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IMethodSymbol method } =>
                method.RefKind != RefKind.None,
            _ => false
        };
    }

    private static bool MayBeReference(ITypeSymbol? type)
    {
        return LocalAliases.MayBeReference(type);
    }

    private static bool IsBoxing(IConversionOperation conversion)
    {
        return conversion.Operand.Type is { IsValueType: true } && MayBeReference(conversion.Type);
    }

    /// <summary>
    ///     The conditional access whose <c>WhenNotNull</c> part contains <paramref name="instance" />; its
    ///     <c>Operation</c> is the value the instance stands for.
    /// </summary>
    private static IConditionalAccessOperation? EnclosingAccess(IOperation instance)
    {
        var previous = instance;
        for (var current = instance.Parent; current is not null; current = current.Parent)
        {
            if (current is IConditionalAccessOperation access && access.WhenNotNull == previous)
                return access;

            previous = current;
        }

        return null;
    }
}