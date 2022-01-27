using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace MicrosoftDI.AddXRefactoring.Provider;

public class CodeActionProvider
{
    private readonly CodeRefactoringContext _context;

    public CodeActionProvider(CodeRefactoringContext context)
    {
        _context = context;
    }
    
    public IEnumerable<CodeAction> PrepareCodeActions(
        TypeDeclarationSyntax typeDeclarationSyntax,
        MethodDeclarationSyntax registrationMethodDeclarationSyntax)
    {
        var serviceCollection = registrationMethodDeclarationSyntax.ParameterList.Parameters.First();
        var possibleAddXInvocations = GetPossibleAddXInvocations(IdentifierName(serviceCollection.Identifier),
            typeDeclarationSyntax);

        return possibleAddXInvocations.Select(tuple =>
            CodeAction.Create(tuple.actionTitle, CreateChangedSolution(registrationMethodDeclarationSyntax, tuple.invocationExpr)));
    }

    private Func<CancellationToken, Task<Solution>> CreateChangedSolution(
        MethodDeclarationSyntax registrationMethod, ExpressionStatementSyntax addXExpr)
    {
        return async token =>
        {
            var doc = _context.Document.Project.GetDocument(registrationMethod.SyntaxTree);
            if (doc == null)
                return _context.Document.Project.Solution;
        
            // var semanticModel = await doc.GetSemanticModelAsync(_context.CancellationToken);
            // if (semanticModel == null)
            //     return _context.Document.Project.Solution;
            //
            // var method = ModelExtensions.GetDeclaredSymbol(semanticModel, registrationMethodDeclarationSyntax) as IMethodSymbol;
            // if (method == null)
            //     return _context.Document.Project.Solution;

            var root = await registrationMethod.SyntaxTree.GetRootAsync(token);
            //root.TrackNodes(registrationMethodDeclarationSyntax);

            var r = registrationMethod;
            SyntaxTriviaList leadingTrivia, trailingTrivia;
            if (r.Body!.Statements.FirstOrDefault() is { } statement)
            {
                leadingTrivia = statement.GetLeadingTrivia();
                trailingTrivia = statement.GetTrailingTrivia();
            }
            else
            {
                leadingTrivia = r.Body.GetLeadingTrivia().Add(Whitespace(" "));
                trailingTrivia = SyntaxTriviaList.Create(LineFeed);
            }
            
            var statements = r.Body!.Statements.Insert(0, 
                addXExpr.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia)
                );
            var newMethodDecl = r.WithBody(r.Body.WithStatements(statements));
            
            var newRoot = root.ReplaceNode(registrationMethod, newMethodDecl);
            return doc.Project.Solution.WithDocumentSyntaxRoot(doc.Id, newRoot);
        };
    }

    private static IEnumerable<(string actionTitle, ExpressionStatementSyntax invocationExpr)> GetPossibleAddXInvocations(
        ExpressionSyntax serviceCollection, 
        BaseTypeDeclarationSyntax typeDeclarationSyntax)
    {
        TypeArgumentListSyntax list;
        if (typeDeclarationSyntax.BaseList?.Types.FirstOrDefault()?.Type is NameSyntax firstBaseType)
        {
            list = TypeArgumentList(
                SeparatedList(new TypeSyntax[]
                {
                    IdentifierName(firstBaseType.WithoutTrivia().ToString()),
                    IdentifierName(typeDeclarationSyntax.Identifier.WithoutTrivia())
                })
            );
        }
        else
        {
            list = TypeArgumentList(
                SeparatedList(new TypeSyntax[]
                {
                    IdentifierName(typeDeclarationSyntax.Identifier.WithoutTrivia())
                })
            );
        }
        
        foreach (var addXMethodName in DefaultMethods)
        {
            yield return (addXMethodName, ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                        serviceCollection, 
                        GenericName(addXMethodName)
                            .WithTypeArgumentList(list)
                    ))
                )
            );
        }
    }

    private static string[] DefaultMethods = new[]
    {
        "AddSingleton",
        "AddScoped",
        "AddTransient"
    };
}