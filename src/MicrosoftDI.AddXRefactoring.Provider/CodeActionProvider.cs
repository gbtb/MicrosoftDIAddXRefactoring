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
    
    public IEnumerable<CodeAction> PrepareCodeActions(TypeDeclarationSyntax typeDeclarationSyntax,
        MethodDeclarationSyntax registrationMethodDeclarationSyntax, List<UsingDirectiveSyntax> usingDirectiveSyntaxes)
    {
        var serviceCollection = registrationMethodDeclarationSyntax.ParameterList.Parameters.First();
         return GetPossibleAddXInvocations(registrationMethodDeclarationSyntax, IdentifierName(serviceCollection.Identifier),
            typeDeclarationSyntax, usingDirectiveSyntaxes);
    }

    private IEnumerable<CodeAction> GetPossibleAddXInvocations(
        MethodDeclarationSyntax registrationMethodDeclarationSyntax,
        ExpressionSyntax serviceCollection,
        BaseTypeDeclarationSyntax typeDeclarationSyntax, List<UsingDirectiveSyntax> usingDirectiveSyntaxes)
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
        
        foreach (var codeAction in GenerateCodeActionsForMethods(registrationMethodDeclarationSyntax, serviceCollection, mainList, usingDirectiveSyntaxes)) 
            yield return codeAction;

        if (additionalList == null) 
            yield break;
        
        var methods = GenerateCodeActionsForMethods(registrationMethodDeclarationSyntax, serviceCollection, additionalList, usingDirectiveSyntaxes);
        yield return CodeAction.Create("Register with ...", methods.ToImmutableArray(), false);
    }

    private IEnumerable<CodeAction> GenerateCodeActionsForMethods(
        MethodDeclarationSyntax registrationMethodDeclarationSyntax,
        ExpressionSyntax serviceCollection, TypeArgumentListSyntax argList,
        List<UsingDirectiveSyntax> usingDirectiveSyntaxes)
    {
        foreach (var addXMethodName in DefaultMethods)
        {
            var methodCallName = GenericName(addXMethodName).WithTypeArgumentList(argList);
            var codeActionTitle = $"Register with {methodCallName.ToFullString()}";

            yield return CodeAction.Create(codeActionTitle,
                CreateChangedSolution(registrationMethodDeclarationSyntax, methodCallName, serviceCollection, usingDirectiveSyntaxes));
        }
    }
    
    private Func<CancellationToken, Task<Solution>> CreateChangedSolution(MethodDeclarationSyntax registrationMethod,
        GenericNameSyntax methodCallName, ExpressionSyntax serviceCollection,
        List<UsingDirectiveSyntax> usingDirectiveSyntaxes)
    {
        return async token =>
        {
            var doc = _context.Document.Project.GetDocument(registrationMethod.SyntaxTree);
            if (doc == null)
                return _context.Document.Project.Solution;
        
            var root = await registrationMethod.SyntaxTree.GetRootAsync(token);

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

            ReturnStatementSyntax? returnStatement = null;
            foreach (var statementSyntax in r.Body.Statements)
            {
                if (statementSyntax is ReturnStatementSyntax ret)
                    returnStatement = ret;
            }

            SyntaxList<StatementSyntax> statements;
            if (returnStatement != null && ContainsAtLeastTwoInvocations(returnStatement))
            {
                statements = r.Body.Statements.Replace(returnStatement, AddIntoCallChain(returnStatement, methodCallName));
            }
            else if (returnStatement != null || r.Body.Statements.Any())
                statements = r.Body!.Statements.Insert(0, 
                    AddStandaloneCall(serviceCollection, methodCallName, leadingTrivia, trailingTrivia)
                );
            else
                statements = r.Body!.Statements.Insert(0, 
                    AddStandaloneCallWithReturn(serviceCollection, methodCallName)
                );
            
            var newMethodDecl = r.WithBody(r.Body.WithStatements(statements));
            
            var newRoot = root.ReplaceNode(registrationMethod, newMethodDecl);

            newRoot = AddUsings(newRoot, registrationMethod, usingDirectiveSyntaxes);
            
            return doc.Project.Solution.WithDocumentSyntaxRoot(doc.Id, newRoot);
        };
    }

    private StatementSyntax AddStandaloneCallWithReturn(ExpressionSyntax serviceCollection, GenericNameSyntax methodCallName)
    {
        var invocation = AddMethodInvocation(serviceCollection, methodCallName);
        return ReturnStatement(invocation);
    }

    private StatementSyntax AddStandaloneCall(ExpressionSyntax serviceCollection, GenericNameSyntax methodCallName,
        SyntaxTriviaList leadingTrivia,
        SyntaxTriviaList trailingTrivia)
    {
        var syntax = ExpressionStatement(
            AddMethodInvocation(serviceCollection, methodCallName)
        );
        return syntax.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
    }

    private static InvocationExpressionSyntax AddMethodInvocation(ExpressionSyntax serviceCollection, GenericNameSyntax methodCallName)
    {
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                serviceCollection,
                methodCallName
            ));
    }

    private StatementSyntax AddIntoCallChain(ReturnStatementSyntax returnStatement, GenericNameSyntax methodCallName)
    {
        var (trailingTrivia, operatorToken) = ExtractTrivia(returnStatement);

        var memberAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                returnStatement.Expression!.WithTrailingTrivia(trailingTrivia), methodCallName)
            .WithOperatorToken(operatorToken); //[....].AddX<IFoo, Foo>

        var invocation = InvocationExpression(memberAccess);
        
        return returnStatement.WithExpression(invocation);
    }

    private (SyntaxTriviaList, SyntaxToken) ExtractTrivia(ReturnStatementSyntax returnStatement)
    {
        if (returnStatement.Expression is not InvocationExpressionSyntax {Expression: MemberAccessExpressionSyntax m})
            return (SyntaxTriviaList.Empty, Token(SyntaxKind.DotToken));
        
        var operatorToken = m.OperatorToken;
        var trailingTrivia = m.Expression.GetTrailingTrivia();

        return (trailingTrivia, operatorToken);

    }

    private bool ContainsAtLeastTwoInvocations(ReturnStatementSyntax returnStatement)
    {
        return returnStatement.Expression is InvocationExpressionSyntax fistInvoke &&
               fistInvoke.Expression is MemberAccessExpressionSyntax m && m.Expression is InvocationExpressionSyntax;
    }

    private SyntaxNode AddUsings(SyntaxNode newRoot, MethodDeclarationSyntax methodDeclarationSyntax,
        IReadOnlyList<UsingDirectiveSyntax> usingDirectiveSyntaxes)
    {
        var containingNamespace = methodDeclarationSyntax.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var requiredUsings = usingDirectiveSyntaxes.ToList();
        
        if (containingNamespace != null)
        {
            var idx = requiredUsings.FindIndex(s => AreEquivalent(s.Name, containingNamespace.Name));
            if (idx >= 0)
            {
                requiredUsings.RemoveAt(idx);
                if (requiredUsings.Count == 0)
                    return newRoot;
            }
        }
            
        var compilationUnit = newRoot.DescendantNodesAndSelf().OfType<CompilationUnitSyntax>().FirstOrDefault();
        if (compilationUnit == null || usingDirectiveSyntaxes.Count == 0)
            return newRoot;

        foreach (var existingUsing in compilationUnit.Usings)
        {
            var idx = requiredUsings.FindIndex(s => AreEquivalent(s.Name, existingUsing.Name));
            if (idx >= 0)
            {
                requiredUsings.RemoveAt(idx);
                if (requiredUsings.Count == 0)
                    return newRoot;
            }
        }

        var newCompilationUnit = compilationUnit.WithUsings(compilationUnit.Usings.AddRange(requiredUsings));
        return newRoot.ReplaceNode(compilationUnit, newCompilationUnit);
    }

    private static string[] DefaultMethods = {
        "AddSingleton",
        "AddScoped",
        "AddTransient"
    };
}