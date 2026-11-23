using System.Collections.Immutable;
using ancplua_fprog.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ancplua_fprog.Rules;

/// <summary>FP0107 reports the written forms of unsafe code in core, including annotated union bodies.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Fp0107UnsafeCodeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = FpDescriptors.Create(
        "FP0107",
        "Do not use pointers or unsafe code in the functional core",
        "Use safe values and operations in the functional core");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.PointerType, SyntaxKind.FunctionPointerType, SyntaxKind.AddressOfExpression,
            SyntaxKind.FixedStatement, SyntaxKind.UnsafeStatement, SyntaxKind.UnsafeExpression,
            SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration, SyntaxKind.UnionDeclaration, SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration, SyntaxKind.DelegateDeclaration, SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement, SyntaxKind.ConstructorDeclaration, SyntaxKind.DestructorDeclaration,
            SyntaxKind.OperatorDeclaration, SyntaxKind.ConversionOperatorDeclaration, SyntaxKind.FieldDeclaration,
            SyntaxKind.EventFieldDeclaration, SyntaxKind.EventDeclaration, SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration, SyntaxKind.ExtensionBlockDeclaration,
            SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.AnonymousMethodExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!CoreScope.IsInCore(context.ContainingSymbol))
            return;

        var syntax = context.Node;
        // Roslyn also visits union contents under each surrounding type. A true type-owned pass
        // applies to that type's own declaration or header. Member bodies have their member as owner.
        if (context.ContainingSymbol is INamedTypeSymbol type
            && syntax.FirstAncestorOrSelf<MemberDeclarationSyntax>() is { } declaration
            && !SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken), type))
            return;

        var location = syntax switch
        {
            PointerTypeSyntax or FunctionPointerTypeSyntax => syntax.GetLocation(),
            PrefixUnaryExpressionSyntax address => address.OperatorToken.GetLocation(),
            FixedStatementSyntax statement => statement.FixedKeyword.GetLocation(),
            UnsafeStatementSyntax statement => statement.UnsafeKeyword.GetLocation(),
            UnsafeExpressionSyntax expression => expression.Keyword.GetLocation(),
            _ => ModifierLocation(syntax)
        };
        if (location is not null)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static Location? ModifierLocation(SyntaxNode syntax)
    {
        foreach (var token in syntax.ChildTokens())
            if (token.IsKind(SyntaxKind.UnsafeKeyword))
                return token.GetLocation();

        return null;
    }
}
