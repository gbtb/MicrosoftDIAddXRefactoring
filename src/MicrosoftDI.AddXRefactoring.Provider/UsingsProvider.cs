using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MicrosoftDI.AddXRefactoring.Provider;

public class UsingsProvider
{
    public static List<UsingDirectiveSyntax> GetUsings(INamespaceSymbol typeNamespaceSymbol, INamespaceSymbol? baseNamespaceSymbol)
    {
        var result = new List<UsingDirectiveSyntax>(2);
        
        if (!typeNamespaceSymbol.IsGlobalNamespace)
            GetUsingSyntax(typeNamespaceSymbol, result);
        
        if (baseNamespaceSymbol is { IsGlobalNamespace: false })
            GetUsingSyntax(baseNamespaceSymbol, result);

        return result;
    }

    private static void GetUsingSyntax(INamespaceSymbol typeNamespaceSymbol,
        List<UsingDirectiveSyntax> result)
    {
        NameSyntax? namespaceName = null;

        foreach (var namespaceSymbol in typeNamespaceSymbol.ConstituentNamespaces)
        {
            if (namespaceName == null)
                namespaceName = IdentifierName(namespaceSymbol.Name);
            else
                namespaceName = QualifiedName(namespaceName, IdentifierName(namespaceSymbol.Name));
        }

        if (namespaceName != null)
            result.Add(UsingDirective(namespaceName));
    }
}