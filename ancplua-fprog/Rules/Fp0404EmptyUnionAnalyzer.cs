using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0404 detects explicit creation of an empty union struct.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0404EmptyUnionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0404",
        "Construct a union from a case",
        "Create '{0}' from one of its cases; an empty union value matches no case and makes a complete switch throw");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.DefaultValue, OperationKind.ObjectCreation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        if (!operation.IsImplicit && operation.Type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsUnion: true } union
            && operation is (IDefaultValueOperation
                or IObjectCreationOperation { Constructor: { IsImplicitlyDeclared: true, Parameters.Length: 0 } })
            && HasAutoValue(union, context.CancellationToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), union.Name));
    }

    private static bool HasAutoValue(INamedTypeSymbol union, CancellationToken cancellationToken)
    {
        var field = union.GetMembers("<Value>k__BackingField").OfType<IFieldSymbol>().FirstOrDefault();
        if (field is not { IsStatic: false, Type.SpecialType: SpecialType.System_Object })
            return false;

        if (field.AssociatedSymbol is IPropertySymbol { GetMethod: { } accessor })
        {
            var getter = accessor.PartialImplementationPart ?? accessor;
            return getter.IsImplicitlyDeclared || getter.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken) is AccessorDeclarationSyntax { Body: null, ExpressionBody: null });
        }

        // Metadata retains the field but does not associate it with the property.
        // A computed getter using the field keyword also has a generated backing field.
        return IsCompilerGenerated(field) && union.GetMembers("Value").OfType<IPropertySymbol>()
            .Any(static property => property.GetMethod is { } getter && IsCompilerGenerated(getter));
    }

    private static bool IsCompilerGenerated(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }
}
