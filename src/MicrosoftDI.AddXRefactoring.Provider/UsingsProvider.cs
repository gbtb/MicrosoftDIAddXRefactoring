using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MicrosoftDI.AddXRefactoring.Provider;

/// <summary>
/// Gets necessary usings for registering type and/or its base type from semantic model
/// </summary>
internal static class UsingsProvider
{
    public static ISet<UsingDirectiveSyntax> GetUsings(INamespaceSymbol typeNamespaceSymbol, INamespaceSymbol? baseNamespaceSymbol)
    {
        var result = new HashSet<UsingDirectiveSyntax>(new Comparer());
        
        if (!typeNamespaceSymbol.IsGlobalNamespace)
            GetUsingSyntax(typeNamespaceSymbol, result);
        
        if (baseNamespaceSymbol is { IsGlobalNamespace: false })
            GetUsingSyntax(baseNamespaceSymbol, result);

        return result;
    }

    private static void GetUsingSyntax(INamespaceSymbol typeNamespaceSymbol,
        HashSet<UsingDirectiveSyntax> result)
    {
        NameSyntax? namespaceName = null;

        
        foreach (var part in typeNamespaceSymbol.ToDisplayParts())
        {
            if (part.ToString() == ".")
                continue;
            
            if (namespaceName == null)
                namespaceName = IdentifierName(part.ToString());
            else
                namespaceName = QualifiedName(namespaceName, IdentifierName(part.ToString()));
        }

        if (namespaceName != null)
            result.Add(UsingDirective(namespaceName));
    }

    private class Comparer : IEqualityComparer<UsingDirectiveSyntax>
    {
        public bool Equals(UsingDirectiveSyntax? x, UsingDirectiveSyntax? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            
            return AreEquivalent(x, y);
        }

        public int GetHashCode(UsingDirectiveSyntax obj)
        {
            return obj.Name.ToFullString().GetHashCode();
        }
    }
}