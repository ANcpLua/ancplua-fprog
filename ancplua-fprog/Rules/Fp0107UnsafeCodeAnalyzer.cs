#pragma warning disable RSEXPERIMENTAL006 // FP0107 also checks the memory-safety preview's unsafe(expr) syntax.

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
        "Use Span<T>, ReadOnlySpan<T> or Memory<T> in place of pointers and unsafe code, and keep interop in the shell");

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
