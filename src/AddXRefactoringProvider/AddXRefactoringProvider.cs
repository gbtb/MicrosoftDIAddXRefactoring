using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AddXRefactoringProvider;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AddXRefactoringProvider))]
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

    private static async Task<MethodDeclarationSyntax?> FindNearestRegistrationMethodAsync(CodeRefactoringContext context)
    {
        var folders = new Stack<string>(context.Document.Folders);
        folders.Push("");

        do
        {
            if (folders.Count > 0)
                folders.Pop();
            
            var method =
                await ScanFolderForRegistrationMethodAsync(folders, context.Document.Project,
                    context.CancellationToken);
            
            if (method != null)
                return method;
        } while (folders.Any());

        return null;
    }

    private static async Task<MethodDeclarationSyntax?> ScanFolderForRegistrationMethodAsync(IEnumerable<string> folders, Project project, CancellationToken token)
    {
        var docs = project.Documents.Where(d => d.Folders.SequenceEqual(folders));
        foreach (var doc in docs)
        {
            var root = await doc.GetSyntaxRootAsync(token);
            if (root == null)
                continue;

            MethodDeclarationSyntax? method;
            if (doc.Name == "Startup.cs")
                method = TryGetConfigureServices(root);
            else
                method = ScanDocumentForRegistrationMethod(root, CheckConventionalMethodSignature);
            
            if (method != null)
                return method;
        }

        return null;
    }

    private static MethodDeclarationSyntax? TryGetConfigureServices(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (node is not ClassDeclarationSyntax {Identifier: {ValueText: "Startup"}})
                continue;

            return ScanDocumentForRegistrationMethod(node, CheckConfigureServicesMethod);
        }

        return null;
    }

    private static bool CheckConfigureServicesMethod(MethodDeclarationSyntax method)
    {
        return method.Identifier.ValueText == "ConfigureServices" &&
               TypeIsIServiceCollection(method.ParameterList.Parameters.First().Type);
    }

    private static MethodDeclarationSyntax? ScanDocumentForRegistrationMethod(SyntaxNode root, Func<MethodDeclarationSyntax, bool> checkMethodSignature)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (node is not MethodDeclarationSyntax decl) 
                continue;

            foreach (var attrList in decl.AttributeLists)
            {
                foreach (var attribute in attrList.Attributes)
                {
                    if (attribute.Name.ToString().Contains("IgnoreRegistrationMethod"))
                        goto Next;

                    if (attribute.Name.ToString().Contains("RegistrationMethod"))
                        return decl;
                }
            }

            if (checkMethodSignature(decl))
                return decl;
            
            Next:
                continue;
        }

        return null;
    }

    private static bool CheckConventionalMethodSignature(MethodDeclarationSyntax decl)
    {
        return TypeIsIServiceCollection(decl.ReturnType)
               && decl.Modifiers.IndexOf(SyntaxKind.StaticKeyword) >= 0
               && TypeIsIServiceCollection(decl.ParameterList.Parameters.First().Type)
               && decl.ParameterList.Parameters.First().Modifiers.IndexOf(SyntaxKind.ThisKeyword) >= 0;
    }

    private static bool TypeIsIServiceCollection(TypeSyntax? type)
    {
        return type is IdentifierNameSyntax { Identifier.Text: "IServiceCollection"};
    }

    private static async Task<TypeDeclarationSyntax?> TryGetValidSyntaxToSuggestRefactoringAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var node = root?.FindToken(context.Span.Start).Parent;
        TypeDeclarationSyntax declaration;
        if (node is TypeDeclarationSyntax decl)
            declaration = decl;
        // else if (node?.Parent is TypeDeclarationSyntax decl2)
        //     declaration = decl2;
        else
            return null;
        
        //generics are harder, maybe later
        if (declaration.Arity == 0 && declaration.Modifiers.IndexOf(SyntaxKind.StaticKeyword) < 0)
            return declaration;

        return null;
    }
}