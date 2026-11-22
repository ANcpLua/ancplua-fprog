using System.Collections.Immutable;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>
/// FP0102: a hidden input inside the functional core. Reports <c>DateTime.Now/UtcNow/Today</c>,
/// <c>DateTimeOffset.Now/UtcNow</c>, <c>Guid.NewGuid</c>, <c>TimeProvider.System</c>, any member of
/// <c>System.Environment</c> or <c>System.Random</c>, <c>new Random(...)</c>, and every reference to a
/// static field that is neither <c>readonly</c> nor <c>const</c>, also when a <c>dynamic</c> argument
/// defers binding to run time. Reported at the member's name token (inside any parentheses), or at
/// <c>Random</c> in the object creation. A target-typed <c>new(...)</c> that creates a <c>Random</c>
/// has no <c>Random</c> token; under the stricter reading it is still <c>new Random(...)</c> and is
/// reported at its <c>new</c> keyword. The message names the parameter to take instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0102HiddenInputAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0102",
        "Do not read hidden inputs in the functional core",
        "{0} instead of reading '{1}' here");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            Analyze,
            OperationKind.PropertyReference,
            OperationKind.Invocation,
            OperationKind.MethodReference,
            OperationKind.FieldReference,
            OperationKind.ObjectCreation,
            OperationKind.DynamicMemberReference,
            OperationKind.DynamicObjectCreation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = context.Operation;

        if (!IsHiddenInput(operation, context.CancellationToken)
            || IsInsideNameOf(operation)
            || !CoreScope.IsInCore(context.ContainingSymbol))
        {
            return;
        }

        var location = ReportLocation(operation.Syntax);
        var (advice, member) = Describe(operation);
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, advice, member));
    }

    private const string RandomAdvice =
        "Take the random value as a parameter (for example 'int roll' or 'double sample') produced by the shell";

    /// <summary>What to write instead, and the member that was read, for the diagnostic message.</summary>
    private static (string Advice, string Member) Describe(IOperation operation) => operation switch
    {
        IFieldReferenceOperation reference => (
            "Take the value as a parameter (or make the field 'static readonly'; to change it, return the new value)",
            reference.Field.ContainingType.Name + "." + reference.Field.Name),
        IObjectCreationOperation or IDynamicObjectCreationOperation => (RandomAdvice, "new Random(...)"),
        _ => DescribeMember(MemberOf(operation))
    };

    private static ISymbol? MemberOf(IOperation operation) => operation switch
    {
        IPropertyReferenceOperation reference => reference.Property,
        IInvocationOperation invocation => invocation.TargetMethod,
        IMethodReferenceOperation reference => reference.Method,
        _ => BoundMember(operation)
    };

    /// <summary>The method a <c>dynamic</c> call binds to: the single symbol, or else the first candidate.</summary>
    private static ISymbol? BoundMember(IOperation operation)
    {
        if (operation.SemanticModel is not { } model)
            return null;

        var info = model.GetSymbolInfo(operation.Syntax);
        return info.Symbol ?? (info.CandidateSymbols is { Length: > 0 } candidates ? candidates[0] : null);
    }

    private static (string Advice, string Member) DescribeMember(ISymbol? member)
    {
        var typeName = member?.ContainingType?.Name ?? "";
        var name = member is null ? "" : typeName + "." + member.Name;
        return typeName switch
        {
            "DateTime" or "DateTimeOffset" or "TimeProvider" => (
                "Take the current time as a 'DateTimeOffset now' parameter (or a 'TimeProvider clock' parameter)", name),
            "Guid" => ("Take the new id as a 'Guid id' parameter", name),
            "Environment" => (
                "Take the value as a parameter (for example 'string newLine', or a settings record built by the shell)",
                name),
            _ => (RandomAdvice, name)
        };
    }

    private static bool IsHiddenInput(IOperation operation, CancellationToken cancellationToken) => operation switch
    {
        IPropertyReferenceOperation reference => IsHiddenProperty(reference.Property),
        IInvocationOperation invocation => IsHiddenMethod(invocation.TargetMethod),
        IMethodReferenceOperation reference => IsHiddenMethod(reference.Method),
        IFieldReferenceOperation reference => IsMutableStaticField(reference.Field),
        IObjectCreationOperation creation => IsSystemType(creation.Type, "Random"),
        IDynamicMemberReferenceOperation reference => HasHiddenCandidate(reference, cancellationToken),
        IDynamicObjectCreationOperation creation => IsSystemType(creation.Type, "Random"),
        _ => false
    };

    private static bool IsHiddenProperty(IPropertySymbol property)
    {
        var type = property.ContainingType;
        return IsSystemType(type, "Environment")
               || IsSystemType(type, "Random")
               || (IsSystemType(type, "DateTime") && property.Name is "Now" or "UtcNow" or "Today")
               || (IsSystemType(type, "DateTimeOffset") && property.Name is "Now" or "UtcNow")
               || (IsSystemType(type, "TimeProvider") && property.Name == "System");
    }

    private static bool IsHiddenMethod(IMethodSymbol method)
    {
        // Constructors are covered by the object creation; a subclass's base(...) call is not a creation.
        if (method.MethodKind == MethodKind.Constructor)
            return false;

        if (IsSystemType(method.ContainingType, "Environment")
            || (IsSystemType(method.ContainingType, "Guid") && method.Name == "NewGuid"))
        {
            return true;
        }

        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            if (IsSystemType(current.ContainingType, "Random"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A call bound at run time because an argument is <c>dynamic</c>, such as <c>random.Next(d)</c> or
    /// <c>Environment.GetEnvironmentVariable(d)</c>. The compiler still knows the candidate methods; a
    /// <c>dynamic</c> receiver has none and is not reported.
    /// </summary>
    private static bool HasHiddenCandidate(IOperation reference, CancellationToken cancellationToken)
    {
        if (reference.SemanticModel is not { } model)
            return false;

        var info = model.GetSymbolInfo(reference.Syntax, cancellationToken);
        if (info.Symbol is IMethodSymbol method)
            return IsHiddenMethod(method);

        foreach (var candidate in info.CandidateSymbols)
        {
            if (candidate is IMethodSymbol candidateMethod && IsHiddenMethod(candidateMethod))
                return true;
        }

        return false;
    }

    private static bool IsMutableStaticField(IFieldSymbol field)
        => field is { IsStatic: true, IsConst: false, IsReadOnly: false, IsImplicitlyDeclared: false };

    private static bool IsSystemType(ITypeSymbol? type, string name)
        => type is INamedTypeSymbol
           {
               ContainingType: null,
               ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
           } named
           && named.Name == name;

    private static bool IsInsideNameOf(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is INameOfOperation)
                return true;
        }

        return false;
    }

    private static Location ReportLocation(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax invocation => ReportLocation(invocation.Expression),
        ParenthesizedExpressionSyntax parenthesized => ReportLocation(parenthesized.Expression),
        MemberAccessExpressionSyntax memberAccess => ReportLocation(memberAccess.Name),
        MemberBindingExpressionSyntax memberBinding => ReportLocation(memberBinding.Name),
        ObjectCreationExpressionSyntax creation => ReportLocation(creation.Type),
        ImplicitObjectCreationExpressionSyntax creation => creation.NewKeyword.GetLocation(),
        QualifiedNameSyntax qualified => ReportLocation(qualified.Right),
        AliasQualifiedNameSyntax aliasQualified => ReportLocation(aliasQualified.Name),
        SimpleNameSyntax name => name.Identifier.GetLocation(),
        _ => syntax.GetLocation()
    };
}
