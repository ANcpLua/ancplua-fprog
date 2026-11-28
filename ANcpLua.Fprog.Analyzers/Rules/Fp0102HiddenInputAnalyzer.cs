using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0102 reports members that read ambient values or expose mutable global state.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0102HiddenInputAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0102",
        "Do not read hidden inputs in the functional core",
        "Take the value of '{0}' as a parameter that the shell supplies instead of reading it here; keep static state readonly and return the new value");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.PropertyReference, OperationKind.FieldReference,
            OperationKind.Invocation, OperationKind.MethodReference);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var operation = context.Operation;
        var member = operation switch
        {
            IPropertyReferenceOperation property => (ISymbol)property.Property,
            IFieldReferenceOperation field => field.Field,
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation reference => reference.Method,
            _ => null
        };

        if (member is null)
            return;

        var hash = operation is IInvocationOperation && member.Name == "GetHashCode"
                   || MemberSyntax.IsType(member.ContainingType, "System", "HashCode");
        if (hash ? IsInHashCodeOverride(context.ContainingSymbol) : !IsHiddenPureMember(context, member))
            return;

        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
            if (parent is INameOfOperation)
                return;

        var name = MemberSyntax.NameToken(operation.Syntax);
        if (name.RawKind != 0)
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), name.ValueText));
    }

    private static bool IsInHashCodeOverride(ISymbol? symbol)
    {
        for (var current = symbol; current is IMethodSymbol method; current = current.ContainingSymbol)
            if (method is { Name: "GetHashCode", IsOverride: true, Parameters.Length: 0 })
                return true;

        return false;
    }

    private static bool IsHiddenPureMember(OperationAnalysisContext context, ISymbol member)
    {
        var mutable = member is IFieldSymbol
            { IsStatic: true, IsConst: false, IsReadOnly: false, IsImplicitlyDeclared: false }
            or IPropertySymbol { IsStatic: true, SetMethod.IsInitOnly: false };
        if (!mutable && !IsAmbientPlatformMember(member))
            return false;

        return Purity.Classify(member.ContainingType, context.Options.AnalyzerConfigOptionsProvider,
            context.Operation.Syntax.SyntaxTree) == PurityClass.Pure;
    }

    private static bool IsAmbientPlatformMember(ISymbol member)
    {
        var type = member.ContainingType;
        if (type is not { ContainingType: null })
            return false;

        return (type.Name, member.Name) switch
        {
            ("DateTime", "Now" or "UtcNow" or "Today" or "ToLocalTime" or "ToUniversalTime")
                or ("DateTimeOffset", "Now" or "UtcNow" or "ToLocalTime" or "LocalDateTime")
                or ("TimeZoneInfo", "Local" or "FindSystemTimeZoneById" or "TryFindSystemTimeZoneById" or "GetSystemTimeZones")
                or ("Guid", "NewGuid" or "CreateVersion7")
                or ("StringComparer" or "StringComparison", "CurrentCulture" or "CurrentCultureIgnoreCase")
                    => type.ContainingNamespace.ToDisplayString() == "System",
            ("CultureInfo", "CurrentCulture" or "CurrentUICulture" or "DefaultThreadCurrentCulture"
                or "DefaultThreadCurrentUICulture" or "InstalledUICulture")
                or ("RegionInfo", "CurrentRegion")
                or ("NumberFormatInfo" or "DateTimeFormatInfo", "CurrentInfo")
                    => type.ContainingNamespace.ToDisplayString() == "System.Globalization",
            _ => false
        };
    }
}
