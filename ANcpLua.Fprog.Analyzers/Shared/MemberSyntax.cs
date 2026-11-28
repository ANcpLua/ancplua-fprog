using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ancplua_fprog.Shared;

internal static class MemberSyntax
{
    public static SyntaxToken NameToken(SyntaxNode syntax)
    {
        return syntax switch
        {
            InvocationExpressionSyntax invocation => NameToken(invocation.Expression),
            ParenthesizedExpressionSyntax parenthesized => NameToken(parenthesized.Expression),
            MemberAccessExpressionSyntax access => access.Name.Identifier,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier,
            SimpleNameSyntax name => name.Identifier,
            _ => default
        };
    }

    public static bool IsType(ITypeSymbol? type, string namespaceName, string name)
    {
        return type is INamedTypeSymbol { ContainingType: null } named
               && named.Name == name && named.ContainingNamespace.ToDisplayString() == namespaceName;
    }
}
