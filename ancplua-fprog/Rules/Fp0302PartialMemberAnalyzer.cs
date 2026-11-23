using System;
using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ancplua_fprog.Rules;

/// <summary>Reports uses of partial APIs and operations with total alternatives in the functional core.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0302PartialMemberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0302",
        "Use total operations in the functional core",
        "'{0}' fails for some inputs; use its total sibling (TryParse, FirstOrDefault, TryGetValue, a list pattern, a type pattern) or guard it, and return the failure as a value");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze,
            OperationKind.Invocation, OperationKind.MethodReference, OperationKind.PropertyReference,
            OperationKind.ArrayElementReference, OperationKind.ImplicitIndexerReference, OperationKind.BinaryOperator,
            OperationKind.CompoundAssignment, OperationKind.Conversion);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol) || InsideNameOf(context.Operation))
            return;

        switch (context.Operation)
        {
            case IInvocationOperation { Syntax: InvocationExpressionSyntax syntax } invocation:
                AnalyzeMethodUse(context, invocation.TargetMethod, invocation.ConstrainedToType, syntax.Expression);
                break;
            case IMethodReferenceOperation { Syntax: ExpressionSyntax syntax } reference:
                AnalyzeMethodUse(context, reference.Method, reference.ConstrainedToType, syntax);
                break;
            case IPropertyReferenceOperation reference:
                AnalyzePropertyUse(context, reference);
                break;
            case IArrayElementReferenceOperation array:
                AnalyzePosition(context, array.ArrayReference, array.Indices);
                break;
            case IImplicitIndexerReferenceOperation indexer when IsPositionalType(indexer.IndexerSymbol.ContainingType):
                AnalyzePosition(context, indexer.Instance, [indexer.Argument]);
                break;
            case IBinaryOperation { Syntax: BinaryExpressionSyntax syntax } binary:
                AnalyzeDivision(context, binary.OperatorKind, binary.LeftOperand, binary.RightOperand, syntax.OperatorToken);
                break;
            case ICompoundAssignmentOperation { Syntax: AssignmentExpressionSyntax syntax } compound:
                AnalyzeDivision(context, compound.OperatorKind, compound.Target, compound.Value, syntax.OperatorToken);
                break;
            case IConversionOperation
                {
                    IsImplicit: false,
                    Syntax: CastExpressionSyntax cast,
                    Operand.Type: INamedTypeSymbol { SpecialType: not SpecialType.System_Object } source,
                    Type: INamedTypeSymbol destination
                } when IsPartialCast(context.Compilation, source, destination):
                Report(context, cast.GetLocation(), "cast");
                break;
        }
    }

    private static bool InsideNameOf(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
            if (parent is INameOfOperation)
                return true;

        return false;
    }

    private static void AnalyzeMethodUse(OperationAnalysisContext context, IMethodSymbol method, ITypeSymbol? constrainedTo,
        ExpressionSyntax expression)
    {
        if (IsPartialMethod(context, method, constrainedTo) && NameToken(expression) is { RawKind: (int)SyntaxKind.IdentifierToken } name)
            Report(context, name.GetLocation(), method.Name);
    }

    private static bool IsPartialMethod(OperationAnalysisContext context, IMethodSymbol method, ITypeSymbol? constrainedTo)
    {
        var type = method.ContainingType;
        if (method.Name is "Parse" or "ParseExact" && method.IsStatic)
            return constrainedTo is ITypeParameterSymbol
                   || Purity.Classify(type, context.Options.AnalyzerConfigOptionsProvider, context.Operation.Syntax.SyntaxTree)
                   == PurityClass.Pure;

        if (type is null)
            return false;

        if (IsSystemType(type, "Convert"))
        {
            return method.Name switch
            {
                "ToString" or "ToBase64String" or "ToBase64CharArray" or "ToHexString" or "ToHexStringLower" => false,
                "ChangeType" => true,
                _ => method.Name.StartsWith("To", StringComparison.Ordinal) || method.Name.StartsWith("From", StringComparison.Ordinal)
            };
        }

        if (IsLinqOperatorClass(type, out var isEnumerable))
        {
            return method.Name switch
            {
                "First" or "Last" or "Single" or "ElementAt" => true,
                "Aggregate" => isEnumerable && (method.ReducedFrom ?? method).Parameters.Length == 2,
                "Min" or "Max" or "Average" or "MinBy" or "MaxBy" => method.ReturnType.IsValueType
                                                                    && method.ReturnType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T,
                _ => false
            };
        }

        return method.Name is "Dequeue" or "Peek" or "Pop" && IsInSystemCollections(type);
    }

    private static bool IsLinqOperatorClass(INamedTypeSymbol type, out bool isEnumerable)
    {
        var outermost = type;
        while (outermost.ContainingType is { } owner)
            outermost = owner;

        isEnumerable = outermost.MetadataName == "Enumerable";
        return (isEnumerable || outermost.MetadataName == "ImmutableArrayExtensions")
               && outermost.ContainingNamespace is { Name: "Linq" } linq
               && IsSystemNamespace(linq.ContainingNamespace);
    }

    private static void AnalyzePropertyUse(OperationAnalysisContext context, IPropertyReferenceOperation reference)
    {
        var property = reference.Property;
        var definition = property.OriginalDefinition;
        if (definition is { Name: "Value", ContainingType.SpecialType: SpecialType.System_Nullable_T })
        {
            if (!PartialUseGuards.GuardsNullableValue(reference)
                && reference.Syntax is ExpressionSyntax expression
                && NameToken(expression) is { RawKind: (int)SyntaxKind.IdentifierToken } name)
                Report(context, name.GetLocation(), name.ValueText);

            return;
        }

        if (!definition.IsIndexer || definition.Parameters.Length != 1)
            return;

        var parameterType = definition.Parameters[0].Type;
        if (IsDictionaryIndexer(definition.ContainingType, parameterType))
        {
            if (!WriteOnlyTarget(reference))
                Report(context, ElementAccessLocation(reference.Syntax), "this[]");

            return;
        }

        if (IsPositionalType(definition.ContainingType) && IsPositionIndex(property.Parameters[0].Type)
            && reference.Arguments.Length == 1)
            AnalyzePosition(context, reference.Instance, [reference.Arguments[0].Value]);
    }

    private static void AnalyzePosition(OperationAnalysisContext context, IOperation? collection, ImmutableArray<IOperation> indices)
    {
        var use = context.Operation;
        if (!WriteOnlyTarget(use) && !PartialUseGuards.GuardsPosition(use, collection, indices))
            Report(context, ElementAccessLocation(use.Syntax), "this[]");
    }

    private static bool IsDictionaryIndexer(INamedTypeSymbol type, ITypeSymbol keyType)
    {
        if (!IsInSystemCollections(type))
            return false;

        if (IsDictionaryInterfaceWithKey(type, keyType))
            return true;

        foreach (var @interface in type.AllInterfaces)
            if (IsDictionaryInterfaceWithKey(@interface, keyType))
                return true;

        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            if (current is
                {
                    ContainingType: null,
                    MetadataName: "KeyedCollection`2",
                    ContainingNamespace: { Name: "ObjectModel", ContainingNamespace: { Name: "Collections" } collections }
                }
                && IsSystemNamespace(collections.ContainingNamespace)
                && SymbolEqualityComparer.Default.Equals(keyType, current.TypeArguments[0]))
                return true;

        return false;
    }

    private static bool IsDictionaryInterfaceWithKey(INamedTypeSymbol type, ITypeSymbol keyType)
    {
        return type is
               {
                   ContainingType: null,
                   MetadataName: "IDictionary`2" or "IReadOnlyDictionary`2",
                   ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections" } collections }
               }
               && IsSystemNamespace(collections.ContainingNamespace)
               && SymbolEqualityComparer.Default.Equals(keyType, type.TypeArguments[0]);
    }

    private static bool IsPositionalType(ITypeSymbol? type)
    {
        return type is IArrayTypeSymbol || type?.SpecialType == SpecialType.System_String
               || (type is INamedTypeSymbol named && (IsInSystemCollections(named)
                   || IsSystemType(named, "Span`1") || IsSystemType(named, "ReadOnlySpan`1")));
    }

    private static bool IsPositionIndex(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Int32 || IsSystemType(type, "Index") || IsSystemType(type, "Range");
    }

    private static bool IsInSystemCollections(INamedTypeSymbol type)
    {
        for (var ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
            if (ns.Name == "Collections" && IsSystemNamespace(ns.ContainingNamespace))
                return true;

        return false;
    }

    private static bool IsSystemType(ITypeSymbol type, string metadataName)
    {
        return type.ContainingType is null && type.MetadataName == metadataName && IsSystemNamespace(type.ContainingNamespace);
    }

    private static bool IsSystemNamespace(INamespaceSymbol? ns)
    {
        return ns is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
    }

    private static void AnalyzeDivision(OperationAnalysisContext context, BinaryOperatorKind kind, IOperation left,
        IOperation divisor, SyntaxToken token)
    {
        if (kind is BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder
            && IsIntegralOrDecimal(left.Type) && IsIntegralOrDecimal(divisor.Type)
            && !IsKnownNonzero(divisor)
            && !PartialUseGuards.GuardsDivisor(context.Operation, divisor))
            Report(context, token.GetLocation(), token.ValueText);
    }

    private static bool IsIntegralOrDecimal(ITypeSymbol? type)
    {
        var valueType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        return valueType?.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte
                   or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
                   or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
                   or SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_Decimal or SpecialType.System_Char
               || IsWideInteger(valueType);
    }

    private static bool IsWideInteger(ITypeSymbol? type)
    {
        return type is not null && (IsSystemType(type, "Int128") || IsSystemType(type, "UInt128")
                                    || type is { Name: "BigInteger", ContainingType: null, ContainingNamespace: { Name: "Numerics" } numerics }
                                    && IsSystemNamespace(numerics.ContainingNamespace));
    }

    private static bool IsKnownNonzero(IOperation value)
    {
        if (value.ConstantValue is { HasValue: true, Value: var constant })
        {
            return constant switch
            {
                IntPtr signed => signed != IntPtr.Zero,
                UIntPtr unsigned => unsigned != UIntPtr.Zero,
                (sbyte)0 or (byte)0 or (short)0 or (ushort)0 or 0 or 0U or 0L or 0UL or 0m or '\0' => false,
                _ => true
            };
        }

        if (value is not IConversionOperation { Operand: { Type: { } operandType } operand } conversion)
            return false;

        if (conversion.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && SymbolEqualityComparer.Default.Equals(nullable.TypeArguments[0], operandType))
            return IsKnownNonzero(operand);

        return IsWideInteger(conversion.Type) && conversion.Conversion.IsImplicit && IsIntegralOrDecimal(operandType)
               && IsKnownNonzero(operand);
    }

    private static bool IsPartialCast(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
    {
        return compilation.ClassifyConversion(source, destination) is { IsExplicit: true, IsReference: true } or { IsUnboxing: true };
    }

    private static bool WriteOnlyTarget(IOperation reference)
    {
        if (reference.Parent is ISimpleAssignmentOperation simple)
            return simple.Target == reference;

        if (reference.Parent is IArgumentOperation { Parameter.RefKind: RefKind.Out })
            return true;

        var current = reference;
        while (current.Parent is ITupleOperation tuple)
            current = tuple;

        return current != reference
               && current.Parent is IDeconstructionAssignmentOperation deconstruction
               && deconstruction.Target == current;
    }

    private static SyntaxToken NameToken(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => NameToken(parenthesized.Expression),
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier,
            SimpleNameSyntax simpleName => simpleName.Identifier,
            _ => default
        };
    }

    private static Location ElementAccessLocation(SyntaxNode syntax)
    {
        var start = syntax.SpanStart;
        var current = syntax;
        while (current.Parent is { } parent)
        {
            if (parent is ConditionalAccessExpressionSyntax conditional && conditional.WhenNotNull == current)
                start = parent.SpanStart;
            else if (parent.SpanStart != current.SpanStart)
                break;

            current = parent;
        }

        return Location.Create(syntax.SyntaxTree, TextSpan.FromBounds(start, syntax.Span.End));
    }

    private static void Report(OperationAnalysisContext context, Location location, string member)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, member));
    }
}
