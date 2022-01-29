using System.Collections.Immutable;
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
         return GetPossibleAddXInvocations(registrationMethodDeclarationSyntax, IdentifierName(serviceCollection.Identifier),
            typeDeclarationSyntax);
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

    private IEnumerable<CodeAction> GetPossibleAddXInvocations(MethodDeclarationSyntax registrationMethodDeclarationSyntax,
            ExpressionSyntax serviceCollection,
            BaseTypeDeclarationSyntax typeDeclarationSyntax)
    {
        TypeArgumentListSyntax mainList;
        TypeArgumentListSyntax? additionalList;
        if (typeDeclarationSyntax.BaseList?.Types.FirstOrDefault()?.Type is NameSyntax firstBaseType)
        {
            mainList = TypeArgumentList(
                SeparatedList(new TypeSyntax[]
                {
                    IdentifierName(firstBaseType.WithoutTrivia().ToString()),
                    IdentifierName(typeDeclarationSyntax.Identifier.WithoutTrivia())
                })
            );
            additionalList = TypeArgumentList(
                SeparatedList(new TypeSyntax[]
                {
                    IdentifierName(typeDeclarationSyntax.Identifier.WithoutTrivia())
                })
            );
        }
        else
        {
            mainList = TypeArgumentList(
                SeparatedList(new TypeSyntax[]
                {
                    IdentifierName(typeDeclarationSyntax.Identifier.WithoutTrivia())
                })
            );
            additionalList = default;
        }
        
        foreach (var codeAction in GenerateCodeActionsForMethods(registrationMethodDeclarationSyntax, serviceCollection, mainList)) 
            yield return codeAction;

        if (additionalList == null) 
            yield break;
        
        var methods = GenerateCodeActionsForMethods(registrationMethodDeclarationSyntax, serviceCollection, additionalList);
        yield return CodeAction.Create("Register with ...", methods.ToImmutableArray(), false);
    }

    private IEnumerable<CodeAction> GenerateCodeActionsForMethods(MethodDeclarationSyntax registrationMethodDeclarationSyntax,
        ExpressionSyntax serviceCollection, TypeArgumentListSyntax argList)
    {
        foreach (var addXMethodName in DefaultMethods)
        {
            var methodCallName = GenericName(addXMethodName).WithTypeArgumentList(argList);
            var codeActionTitle = $"Register with {methodCallName.ToFullString()}";

            var syntax = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        serviceCollection,
                        methodCallName
                    ))
            );
            yield return CodeAction.Create(codeActionTitle,
                CreateChangedSolution(registrationMethodDeclarationSyntax, syntax));
        }
    }

    private static string[] DefaultMethods = {
        "AddSingleton",
        "AddScoped",
        "AddTransient"
    };
}