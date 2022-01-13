using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AddXRefactoringProvider;

public class AddXRefactoringProvider: CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var typeDeclaration = await TryGetValidSyntaxToSuggestRefactoringAsync(context);
        if (typeDeclaration == null)
            return;

        var registrationMethod = await FindNearestRegistrationMethodAsync(context);
        if (registrationMethod?.Body != null)
        {
            var provider = new CodeActionProvider(context);
            var codeActions = provider.PrepareCodeActions(typeDeclaration, registrationMethod);
            foreach (var codeAction in codeActions)
            {
                context.RegisterRefactoring(codeAction);
            }
        }
    }

    private async Task<MethodDeclarationSyntax?> FindNearestRegistrationMethodAsync(CodeRefactoringContext context)
    {
        var folders = context.Document.Folders.ToList();
        while (folders.Any())
        {
            var method = await ScanFolderForRegistrationMethodAsync(folders, context.Document.Project, context.CancellationToken);
            if (method != null)
                return method;
            
            folders.RemoveAt(folders.Count - 1);
        }

        return null;
    }

    private async Task<MethodDeclarationSyntax?> ScanFolderForRegistrationMethodAsync(List<string> folders, Project project, CancellationToken token)
    {
        var docs = project.Documents.Where(d => d.Folders.SequenceEqual(folders));
        foreach (var doc in docs)
        {
            var root = await doc.GetSyntaxRootAsync(token);
            if (root == null)
                continue;

            var method = ScanDocumentForRegistrationMethod(root);
            if (method != null)
                return method;
        }

        return null;
    }

    private static MethodDeclarationSyntax? ScanDocumentForRegistrationMethod(SyntaxNode root)
    {
        foreach (var node in root.ChildNodes())
        {
            if (node is not MethodDeclarationSyntax decl) 
                continue;

            foreach (var attrList in decl.AttributeLists)
            {
                foreach (var attribute in attrList.Attributes)
                {
                    if (attribute.Name.ToString().Contains("IgnoreRegistrationMethod"))
                        break;

                    if (attribute.Name.ToString().Contains("RegistrationMethod"))
                    {
                        return decl;
                    }
                }
            }

            if (CheckMethodSignature(decl))
                return decl;
        }

        return null;
    }

    private static bool CheckMethodSignature(MethodDeclarationSyntax decl)
    {
        static bool TypeIsIServiceCollection(TypeSyntax? type)
        {
            return type is IdentifierNameSyntax { Identifier.Text: "IServiceCollection"};
        }

        return TypeIsIServiceCollection(decl.ReturnType)
               && decl.Modifiers.IndexOf(SyntaxKind.StaticKeyword) >= 0
               && TypeIsIServiceCollection(decl.ParameterList.Parameters.First().Type)
               && decl.ParameterList.Parameters.First().Modifiers.IndexOf(SyntaxKind.ThisKeyword) >= 0;
    }

    private async Task<TypeDeclarationSyntax?> TryGetValidSyntaxToSuggestRefactoringAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var declaration = root?.FindToken(context.Span.Start).Parent?
            .AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Arity == 0); //generics are harder, maybe later

        return declaration;
    }
}