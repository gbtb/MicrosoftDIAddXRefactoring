using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AddXRefactoringProvider;

public class AddXRefactoringProvider: CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        if (!await IsValidSyntaxToSuggestRefactoringAsync(context))
            return;

        var method = await FindNearestRegistrationMethodAsync(context);
        if (method != null)
            PrepareCodeAction(context.Document, context.)
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
        }

        return null;
    }

    private async Task<bool> IsValidSyntaxToSuggestRefactoringAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var declaration = root?.FindToken(context.Span.Start).Parent?
            .AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        return declaration == null;
    }
}