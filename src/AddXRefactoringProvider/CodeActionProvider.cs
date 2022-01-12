using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AddXRefactoringProvider;

public class CodeActionProvider
{
    public static async Task<IEnumerable<CodeAction>> PrepareCodeActions(CodeRefactoringContext context,
        TypeDeclarationSyntax? typeDeclarationSyntax,
        MethodDeclarationSyntax registrationMethodDeclarationSyntax)
    {
        var doc = context.Document.Project.GetDocument(registrationMethodDeclarationSyntax.SyntaxTree);
        if (doc == null)
            return Enumerable.Empty<CodeAction>();

        var semanticModel = await doc.GetSemanticModelAsync(context.CancellationToken);
        if (semanticModel == null)
            return Enumerable.Empty<CodeAction>();

        var method = ModelExtensions.GetDeclaredSymbol(semanticModel, registrationMethodDeclarationSyntax) as IMethodSymbol;
        if (method == null)
            return Enumerable.Empty<CodeAction>();

        var possibleAddXInvocations = GetPossibleAddXInvocations(typeDeclarationSyntax);
        
        foreach (var VARIABLE in COLLECTION)
        {
            
        }
        
        //var serviceCollectionSymbol = method.Parameters.First();
        registrationMethodDeclarationSyntax.Body.Statements.Insert(0, GetSimpleAddXStatement());
    }

    private static IEnumerable<ExpressionStatementSyntax> GetPossibleAddXInvocations(IdentifierNameSyntax serviceCollection, TypeDeclarationSyntax typeDeclarationSyntax)
    {
        if (typeDeclarationSyntax.BaseList?.Types.FirstOrDefault()?.Type is { } firstBaseType)
        {
               
        }
        else
        {
            var list = TypeArgumentList(
                SeparatedList<TypeSyntax>(new TypeSyntax[]
                {
                    IdentifierName(typeDeclarationSyntax.Identifier)
                })
            );
            yield return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                        serviceCollection, 
                        GenericName("AddX")
                                .WithTypeArgumentList(list)
                    ))
                );
        }
    }
}