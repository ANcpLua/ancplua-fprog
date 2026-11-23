using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ancplua_fprog.Rules;

/// <summary>FP0101: core code declares a void result or creates a delegate with a void result.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0101VoidResultAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0101",
        "Do not use void results in the functional core",
        "'{0}' has a void result; return what it computes instead: the new state as a 'with' copy or a new record, a result value for an outcome that can fail, or a Func delegate in place of an Action delegate");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod,
            SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement, SyntaxKind.OperatorDeclaration);
        context.RegisterSymbolAction(AnalyzeDelegate, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeWrittenDelegateTypes,
            SyntaxKind.TypeArgumentList, SyntaxKind.FunctionPointerParameter, SyntaxKind.Parameter,
            SyntaxKind.MethodDeclaration, SyntaxKind.OperatorDeclaration, SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.LocalFunctionStatement, SyntaxKind.DelegateDeclaration, SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.VariableDeclaration, SyntaxKind.DeclarationExpression, SyntaxKind.ForEachStatement,
            SyntaxKind.DeclarationPattern, SyntaxKind.RecursivePattern, SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration, SyntaxKind.EventDeclaration);
        context.RegisterOperationAction(AnalyzeDelegateCreation, OperationKind.DelegateCreation);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var (returnType, name) = context.Node switch
        {
            MethodDeclarationSyntax method => (method.ReturnType, method.Identifier),
            LocalFunctionStatementSyntax local => (local.ReturnType, local.Identifier),
            OperatorDeclarationSyntax op => (op.ReturnType, op.OperatorToken),
            _ => (null, default)
        };
        if (returnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }
            || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is IMethodSymbol
            { ReturnsVoid: true } symbol && CoreScope.IsDeclaredInSource(symbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), name.ValueText));
    }

    private static void AnalyzeDelegate(SymbolAnalysisContext context)
    {
        if (IsVoidDelegate(context.Symbol as ITypeSymbol)
            && CoreScope.IsInCore(context.Symbol)
            && CoreScope.IsDeclaredInSource(context.Symbol)
            && context.Symbol.Locations is { Length: > 0 } locations && locations[0].IsInSource)
            context.ReportDiagnostic(Diagnostic.Create(Rule, locations[0], context.Symbol.Name));
    }

    private static void AnalyzeWrittenDelegateTypes(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is TypeArgumentListSyntax list)
        {
            foreach (var argument in list.Arguments)
                AnalyzeWrittenType(argument, context);
            return;
        }

        var type = context.Node switch
        {
            FunctionPointerParameterSyntax parameter => parameter.Type,
            DelegateDeclarationSyntax declaration => declaration.ReturnType,
            ParenthesizedLambdaExpressionSyntax lambda => lambda.ReturnType,
            VariableDeclarationSyntax variable => variable.Type,
            DeclarationExpressionSyntax declaration => declaration.Type,
            DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax } pattern => pattern.Type,
            RecursivePatternSyntax { Designation: SingleVariableDesignationSyntax } pattern => pattern.Type,
            _ => WrittenType(context.Node)
        };
        if (type is not null)
            AnalyzeWrittenType(type, context);
    }

    private static void AnalyzeWrittenType(TypeSyntax type, SyntaxNodeAnalysisContext context)
    {
        // Generic arguments and function pointer signatures have their own syntax actions.
        switch (type)
        {
            case QualifiedNameSyntax qualified:
                AnalyzeWrittenType(qualified.Right, context);
                break;
            case AliasQualifiedNameSyntax alias:
                AnalyzeWrittenType(alias.Name, context);
                break;
            case NullableTypeSyntax nullable:
                AnalyzeWrittenType(nullable.ElementType, context);
                break;
            case ArrayTypeSyntax array:
                AnalyzeWrittenType(array.ElementType, context);
                break;
            case RefTypeSyntax reference:
                AnalyzeWrittenType(reference.Type, context);
                break;
            case ScopedTypeSyntax scoped:
                AnalyzeWrittenType(scoped.Type, context);
                break;
            case TupleTypeSyntax tuple:
                foreach (var element in tuple.Elements)
                    AnalyzeWrittenType(element.Type, context);
                break;
            case SimpleNameSyntax { IsVar: false } name when !name.IsPartOfStructuredTrivia()
                                                            && CoreScope.IsInCore(context.ContainingSymbol):
                var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
                if (IsVoidDelegate(symbol as ITypeSymbol))
                    context.ReportDiagnostic(Diagnostic.Create(Rule, name.Identifier.GetLocation(), name.Identifier.ValueText));
                break;
        }
    }

    private static void AnalyzeDelegateCreation(OperationAnalysisContext context)
    {
        // Explicit casts and delegate creations name the resulting type.
        if (context.Operation is not IDelegateCreationOperation { IsImplicit: true, Type: INamedTypeSymbol type } creation
            || !IsVoidDelegate(type) || !CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var token = creation.Target.Syntax switch
        {
            LambdaExpressionSyntax lambda => lambda.ArrowToken,
            AnonymousMethodExpressionSyntax method => method.DelegateKeyword,
            MemberAccessExpressionSyntax access => access.Name.Identifier,
            SimpleNameSyntax name => name.Identifier,
            _ => default
        };
        if (token.RawKind != 0 && !HasWrittenTarget(creation, type, context))
            context.ReportDiagnostic(Diagnostic.Create(Rule, token.GetLocation(), type.Name));
    }

    private static bool HasWrittenTarget(IOperation expression, INamedTypeSymbol type, OperationAnalysisContext context)
    {
        for (var current = expression; current.Parent is { } parent; current = parent)
        {
            switch (parent)
            {
                case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declaration }:
                    return HasWrittenType(declaration.Symbol, type, context.CancellationToken);
                case IFieldInitializerOperation field:
                    return field.InitializedFields.Any(symbol => HasWrittenType(symbol, type, context.CancellationToken));
                case IPropertyInitializerOperation property:
                    return property.InitializedProperties.Any(symbol => HasWrittenType(symbol, type, context.CancellationToken));
                case IAssignmentOperation assignment when assignment.Value == current:
                    return HasWrittenType(ReferencedSymbol(assignment.Target), type, context.CancellationToken);
                case IEventAssignmentOperation assignment when assignment.HandlerValue == current:
                    return HasWrittenType(ReferencedSymbol(assignment.EventReference), type, context.CancellationToken);
                case IArgumentOperation { Parent: IInvocationOperation { IsImplicit: true } }:
                case IInvocationOperation { IsImplicit: true, Parent: IObjectOrCollectionInitializerOperation }:
                case IObjectOrCollectionInitializerOperation:
                    break;
                case IArgumentOperation argument:
                    return HasWrittenType(argument.Parameter, type, context.CancellationToken)
                           || TypeSpelledInArgumentTarget(argument, type, context.CancellationToken);
                case IReturnOperation:
                    return HasWrittenReturnType(parent, type, context);
                case IArrayCreationOperation { Syntax: ArrayCreationExpressionSyntax }:
                case IObjectCreationOperation { Syntax: ObjectCreationExpressionSyntax }:
                    return ContainsDelegateType(parent.Type, type);
                case IConversionOperation when !ContainsDelegateType(parent.Type, type):
                    return false;
                case IConversionOperation or IParenthesizedOperation or IConditionalOperation or ICoalesceOperation
                    or ISwitchExpressionOperation or ISwitchExpressionArmOperation or ITupleOperation
                    or IArrayInitializerOperation or IArrayCreationOperation or ICollectionExpressionOperation
                    or IObjectCreationOperation:
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private static bool HasWrittenReturnType(
        IOperation ret, INamedTypeSymbol type, OperationAnalysisContext context)
    {
        for (var current = ret.Parent; current is not null; current = current.Parent)
        {
            if (current is ILocalFunctionOperation local)
                return HasWrittenType(local.Symbol, type, context.CancellationToken);

            if (current is IAnonymousFunctionOperation anonymous)
                return (anonymous.Syntax is ParenthesizedLambdaExpressionSyntax { ReturnType: not null }
                        && ContainsDelegateType(anonymous.Symbol.ReturnType, type))
                       || (anonymous.Parent is IDelegateCreationOperation enclosing
                           && HasWrittenTarget(enclosing, type, context));
        }

        var owner = context.ContainingSymbol;
        return HasWrittenType((owner as IMethodSymbol)?.AssociatedSymbol ?? owner, type, context.CancellationToken);
    }

    private static bool TypeSpelledInArgumentTarget(
        IArgumentOperation argument, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var target = argument.Parent?.Syntax switch
        {
            InvocationExpressionSyntax call => call.Expression,
            ObjectCreationExpressionSyntax creation => creation.Type,
            _ => null
        };
        if (target is null || argument.SemanticModel is not { } model)
            return false;

        return target.DescendantNodesAndSelf()
            .OfType<TypeArgumentListSyntax>()
            .SelectMany(static list => list.Arguments)
            .Any(syntax => ContainsDelegateType(model.GetTypeInfo(syntax, cancellationToken).Type, type));
    }

    private static ISymbol? ReferencedSymbol(IOperation target)
    {
        return target switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IMemberReferenceOperation member => member.Member,
            _ => null
        };
    }

    private static bool HasWrittenType(ISymbol? symbol, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        if (symbol is null || !CoreScope.IsInCore(symbol))
            return false;

        // A setter or event accessor gets its implicit value parameter from the written member type.
        var original = symbol.OriginalDefinition;
        if (original is IParameterSymbol
            {
                IsImplicitlyDeclared: true,
                ContainingSymbol: IMethodSymbol { AssociatedSymbol: { } member }
            })
            original = member;

        var declared = original switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IEventSymbol evt => evt.Type,
            IMethodSymbol method => method.ReturnType,
            _ => null
        };
        return ContainsDelegateType(declared, type)
               && original.DeclaringSyntaxReferences.Any(reference =>
                   WrittenType(reference.GetSyntax(cancellationToken)) is { IsVar: false });
    }

    private static TypeSyntax? WrittenType(SyntaxNode declaration)
    {
        return declaration switch
        {
            MethodDeclarationSyntax method => method.ReturnType,
            LocalFunctionStatementSyntax local => local.ReturnType,
            OperatorDeclarationSyntax op => op.ReturnType,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type,
            BasePropertyDeclarationSyntax property => property.Type,
            ParameterSyntax parameter => parameter.Type,
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variable } => variable.Type,
            SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax expression } => expression.Type,
            SingleVariableDesignationSyntax { Parent: DeclarationPatternSyntax pattern } => pattern.Type,
            SingleVariableDesignationSyntax { Parent: RecursivePatternSyntax pattern } => pattern.Type,
            ForEachStatementSyntax loop => loop.Type,
            _ => null
        };
    }

    private static bool ContainsDelegateType(ITypeSymbol? type, INamedTypeSymbol expected)
    {
        return type switch
        {
            INamedTypeSymbol named => SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, expected.OriginalDefinition)
                                     || named.TypeArguments.Any(argument => ContainsDelegateType(argument, expected)),
            IArrayTypeSymbol array => ContainsDelegateType(array.ElementType, expected),
            _ => false
        };
    }

    private static bool IsVoidDelegate(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod.ReturnsVoid: true };
    }
}
