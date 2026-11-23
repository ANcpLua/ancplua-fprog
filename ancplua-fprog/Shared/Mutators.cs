using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Shared;

internal enum MutationOwner
{
    Parameter,
    Field
}

/// <summary>Write operations and the bound framework methods that mutate memory.</summary>
internal static class Mutators
{
    private static readonly ImmutableHashSet<string> InstanceWriters = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        // Lists, sets, dictionaries, ObjectModel collections and immutable builders.
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll", "RemoveRange",
        "RemoveWhere", "Clear", "Sort", "Reverse", "SetRange", "Move", "SetAt", "SetValueAtIndex", "TryAdd",
        "UnionWith", "IntersectWith", "ExceptWith", "SymmetricExceptWith", "TrimExcess", "TrimToSize",
        "EnsureCapacity", "Replace", "MoveToImmutable", "DrainToImmutable",
        // Stacks, queues, linked lists and concurrent collections.
        "Push", "PushRange", "Pop", "TryPop", "TryPopRange", "Enqueue", "EnqueueRange", "EnqueueDequeue",
        "Dequeue", "DequeueEnqueue", "TryDequeue", "AddFirst", "AddLast", "AddBefore", "AddAfter",
        "RemoveFirst", "RemoveLast", "TryRemove", "TryTake", "Take", "TryUpdate", "AddOrUpdate", "GetOrAdd",
        "CompleteAdding",
        // BitArray, Array, Span and StringBuilder.
        "Set", "SetAll", "And", "Or", "Xor", "Not", "LeftShift", "RightShift", "SetValue",
        "Fill", "Append", "AppendLine", "AppendFormat", "AppendJoin");

    private static readonly ImmutableArray<OperationKind> WriteKinds =
    [
        OperationKind.SimpleAssignment,
        OperationKind.CompoundAssignment,
        OperationKind.CoalesceAssignment,
        OperationKind.DeconstructionAssignment,
        OperationKind.EventAssignment,
        OperationKind.Increment,
        OperationKind.Decrement,
        OperationKind.Argument,
        OperationKind.Invocation
    ];

    public static void Register(AnalysisContext context, DiagnosticDescriptor rule, MutationOwner owner)
    {
        context.RegisterOperationBlockStartAction(start =>
        {
            if (!CoreScope.IsInCore(start.OwningSymbol))
                return;

            var aliases = new LocalAliases(start.OperationBlocks, start.CancellationToken);
            start.RegisterOperationAction(operation => Analyze(operation, rule, owner, aliases), WriteKinds);
        });
    }

    private static void Analyze(OperationAnalysisContext context, DiagnosticDescriptor rule, MutationOwner owner,
        LocalAliases aliases)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        switch (context.Operation)
        {
            case IAssignmentOperation assignment:
                CheckTarget(assignment.Target, assignment is ISimpleAssignmentOperation { IsRef: true });
                break;
            case IIncrementOrDecrementOperation increment:
                CheckTarget(increment.Target, false);
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                CheckTarget(argument.Value, false);
                break;
            case IEventAssignmentOperation eventAssignment:
                CheckEvent(eventAssignment.EventReference);
                break;
            case IInvocationOperation { Syntax: InvocationExpressionSyntax syntax } invocation:
                var token = NameOf(syntax.Expression);
                if (token.RawKind != 0)
                    foreach (var destination in WrittenValues(invocation))
                    {
                        var roots = new MutationRoots(aliases, owner, context.ContainingSymbol);
                        if (roots.Value(destination).Symbol is not { } root)
                            continue;

                        context.ReportDiagnostic(Diagnostic.Create(rule, token.GetLocation(), root.Name));
                        break;
                    }

                break;
        }

        void CheckTarget(IOperation target, bool rebind)
        {
            target = LocalAliases.UnwrapDeclaration(target);
            if (target is ITupleOperation tuple)
            {
                foreach (var element in tuple.Elements)
                    CheckTarget(element, false);

                return;
            }

            var roots = new MutationRoots(aliases, owner, context.ContainingSymbol);
            var match = roots.Target(target, rebind);
            if (match.Symbol is { } root)
                context.ReportDiagnostic(Diagnostic.Create(rule, TargetLocation(target, match.Start), root.Name));
        }

        void CheckEvent(IOperation target)
        {
            var roots = new MutationRoots(aliases, owner, context.ContainingSymbol);
            var match = roots.Event(target);
            if (match.Symbol is not { } root)
                return;

            var name = NameOf(target.Syntax);
            var location = owner == MutationOwner.Field && name.RawKind != 0
                ? name.GetLocation()
                : TargetLocation(target, match.Start);
            context.ReportDiagnostic(Diagnostic.Create(rule, location, root.Name));
        }
    }

    private static Location TargetLocation(IOperation target, int start)
    {
        var syntax = target.Syntax;
        while (syntax.Parent is ParenthesizedExpressionSyntax parenthesized)
            syntax = parenthesized;

        return Location.Create(syntax.SyntaxTree, TextSpan.FromBounds(Math.Min(start, syntax.SpanStart), syntax.Span.End));
    }

    private static SyntaxToken NameOf(SyntaxNode syntax)
    {
        return syntax switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier,
            SimpleNameSyntax simple => simple.Identifier,
            ParenthesizedExpressionSyntax parentheses => NameOf(parentheses.Expression),
            _ => default
        };
    }

    private static IEnumerable<IOperation> WrittenValues(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.Name is "CopyTo" or "TryCopyTo")
        {
            if (CopyDestination(invocation) is { } destination)
                yield return destination;

            yield break;
        }

        if ((IsInstanceWriter(method) || IsCollectionExtensionWriter(method))
            && CallReceiver(invocation) is { } receiver)
            yield return receiver;

        foreach (var argument in invocation.Arguments)
            if (argument.Parameter is { } parameter && IsStaticWriter(method, parameter))
                yield return argument.Value;
    }

    private static IOperation? CopyDestination(IInvocationOperation invocation)
    {
        IOperation? soleCandidate = null;
        var candidates = 0;
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter
                || (parameter.Ordinal == 0 && invocation.TargetMethod.IsExtensionMethod && invocation.Instance is null)
                || parameter.Name is "source" or "sourceArray" or "src"
                || !(MayBeReference(parameter.Type) || IsMutableView(parameter.Type)))
                continue;

            if (parameter.Name is "destination" or "destinationArray" or "destinationSpan" or "destinationBuffer"
                or "array" or "buffer" or "target" or "dst")
                return argument.Value;

            soleCandidate = argument.Value;
            candidates++;
        }

        return candidates == 1 ? soleCandidate : null;
    }

    private static bool IsInstanceWriter(IMethodSymbol method)
    {
        return !method.IsStatic
               && InstanceWriters.Contains(method.Name)
               && (method.Name != "Reverse" || method.ReturnsVoid)
               && IsMutableReceiver(method.ContainingType);
    }

    private static bool IsCollectionExtensionWriter(IMethodSymbol method)
    {
        return method.IsExtensionMethod
               && method.Name is "TryAdd" or "Remove"
               && method.ContainingType is { Name: "CollectionExtensions", ContainingType: null }
               && IsCollectionsChild(method.ContainingType.ContainingNamespace, "Generic");
    }

    private static bool IsStaticWriter(IMethodSymbol method, IParameterSymbol parameter)
    {
        if (!method.IsStatic)
            return false;

        var type = method.ContainingType;
        if (type.SpecialType == SpecialType.System_Array)
            return method.Name switch
            {
                "Sort" => parameter.Name is "array" or "keys" or "items",
                "Reverse" or "Clear" or "Fill" => parameter.Name == "array",
                "Copy" or "ConstrainedCopy" => parameter.Name == "destinationArray",
                _ => false
            };

        if (IsSystemType(type, "Buffer"))
            return method.Name switch
            {
                "BlockCopy" => parameter.Name == "dst",
                "MemoryCopy" => parameter.Name == "destination",
                "SetByte" => parameter.Name == "array",
                _ => false
            };

        return IsSystemType(type, "MemoryExtensions")
               && method.Name is "Sort" or "Reverse" or "Fill" or "Clear" or "Replace" or "ReplaceAny" or "ReplaceAnyExcept"
               && IsMutableView(parameter.Type)
               && parameter.Name is not "source";
    }

    private static bool IsMutableReceiver(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Array || IsStringBuilder(type) || IsSystemType(type, "Span", 1))
            return true;

        return CollectionBranch(type) switch
        {
            "Immutable" or "Frozen" => IsNestedBuilder(type),
            not null => true,
            _ => false
        };
    }

    public static string? CollectionBranch(ITypeSymbol type)
    {
        INamespaceSymbol? child = null;
        for (var ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            if (IsCollectionsNamespace(ns))
                return child?.Name ?? "";

            child = ns;
        }

        return null;
    }

    public static bool IsCollectionsNamespace(INamespaceSymbol? ns)
    {
        return ns is
        {
            Name: "Collections",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };
    }

    public static bool IsCollectionsChild(INamespaceSymbol? ns, string name)
    {
        return ns?.Name == name && IsCollectionsNamespace(ns.ContainingNamespace);
    }

    public static bool IsNestedBuilder(ITypeSymbol type) => type is { Name: "Builder", ContainingType: not null };

    public static bool IsStringBuilder(ITypeSymbol type)
    {
        return type is
        {
            Name: "StringBuilder",
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "Text",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            }
        };
    }

    public static bool IsSystemType(ITypeSymbol? type, string name, int arity = 0)
    {
        return type is INamedTypeSymbol
               {
                   ContainingType: null,
                   ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
               } named
               && named.Name == name && named.Arity == arity;
    }

    public static bool IsMutableView(ITypeSymbol? type)
    {
        return IsSystemType(type, "Span", 1) || IsSystemType(type, "Memory", 1);
    }

    public static bool IsView(ITypeSymbol? type)
    {
        return IsMutableView(type) || IsSystemType(type, "ReadOnlySpan", 1) || IsSystemType(type, "ReadOnlyMemory", 1);
    }

    public static bool MayBeReference(ITypeSymbol? type)
    {
        return type is { IsValueType: false, TypeKind: not (TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error) };
    }

    public static IOperation? CallReceiver(IInvocationOperation invocation)
    {
        if (invocation.Instance is { } instance)
            return instance;

        if (invocation.TargetMethod.IsExtensionMethod)
            foreach (var argument in invocation.Arguments)
                if (argument.Parameter?.Ordinal == 0)
                    return argument.Value;

        return null;
    }

    public static IOperation? ViewReceiver(IInvocationOperation invocation)
    {
        if (!IsView(invocation.Type))
            return null;

        var method = invocation.TargetMethod;
        if (method.Name == "Slice" && IsView(method.ContainingType))
            return invocation.Instance;

        return method.Name is "AsSpan" or "AsMemory" && IsSystemType(method.ContainingType, "MemoryExtensions")
            ? CallReceiver(invocation)
            : null;
    }

    /// <summary>A collection call that returns or writes one of its reference-typed elements.</summary>
    public static IOperation? ElementReceiver(IInvocationOperation invocation, ITypeSymbol? element)
    {
        if (element is null || !MayBeReference(element) || CallReceiver(invocation) is not { } receiver)
            return null;

        var original = receiver;
        while (receiver is IConversionOperation { Conversion.IsUserDefined: false } conversion)
            receiver = conversion.Operand;

        return IsElement(receiver.Type, element) || IsElement(original.Type, element) ? original : null;
    }

    private static bool IsElement(ITypeSymbol? collection, ITypeSymbol element)
    {
        if (collection is null)
            return false;

        if (collection is IArrayTypeSymbol array)
            return SymbolEqualityComparer.Default.Equals(array.ElementType, element);

        if (collection is INamedTypeSymbol named && MatchesElement(named, element))
            return true;

        foreach (var implemented in collection.AllInterfaces)
            if (MatchesElement(implemented, element))
                return true;

        return false;
    }

    private static bool MatchesElement(INamedTypeSymbol collection, ITypeSymbol element)
    {
        if (collection.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            || (collection.Arity == 1 && IsView(collection)))
            return SymbolEqualityComparer.Default.Equals(collection.TypeArguments[0], element);

        return collection is { Arity: 2, TypeKind: TypeKind.Interface, ContainingType: null }
               && (collection.Name is "IDictionary" or "IReadOnlyDictionary"
                   && IsCollectionsChild(collection.ContainingNamespace, "Generic")
                   || collection.Name == "IImmutableDictionary"
                   && IsCollectionsChild(collection.ContainingNamespace, "Immutable"))
               && SymbolEqualityComparer.Default.Equals(collection.TypeArguments[1], element);
    }

    public static bool IsPrintMembersParameter(IParameterSymbol parameter)
    {
        return IsStringBuilder(parameter.Type)
               && parameter.ContainingSymbol is IMethodSymbol
               {
                   Name: "PrintMembers", IsStatic: false, Parameters.Length: 1,
                   ReturnType.SpecialType: SpecialType.System_Boolean, ContainingType.IsRecord: true
               };
    }
}
