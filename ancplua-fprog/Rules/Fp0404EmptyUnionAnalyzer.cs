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
            && !AssignsValue(operation)
            && HasAutoValue(union, context.CancellationToken))
            context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), union.Name));
    }

    // With an object initializer that assigns Value, the author states what the union holds.
    private static bool AssignsValue(IOperation operation)
    {
        return operation is IObjectCreationOperation { Initializer: { } initializer }
               && initializer.Initializers.Any(static member => member is ISimpleAssignmentOperation
               {
                   Target: IPropertyReferenceOperation { Property.Name: "Value" }
               });
    }

    private static bool HasAutoValue(INamedTypeSymbol union, CancellationToken cancellationToken)
    {
        var value = union.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault(static property =>
            property is { IsStatic: false, Type.SpecialType: SpecialType.System_Object });
        if (value?.GetMethod is not { } accessor)
            return false;

        // Emitted metadata keeps no getter body. The compiler's mark on the getter tells an automatic one.
        if (union.DeclaringSyntaxReferences.IsEmpty)
            return IsCompilerGenerated(accessor);

        // A computed getter that uses the field keyword has a backing field too, so the getter decides.
        var getter = accessor.PartialImplementationPart ?? accessor;
        return union.GetMembers().OfType<IFieldSymbol>().Any(static field =>
                   field.AssociatedSymbol is IPropertySymbol { Name: "Value" })
               && (getter.IsImplicitlyDeclared || getter.DeclaringSyntaxReferences.Any(reference =>
                   reference.GetSyntax(cancellationToken) is AccessorDeclarationSyntax { Body: null, ExpressionBody: null }));
    }

    private static bool IsCompilerGenerated(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }
}
