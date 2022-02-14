using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AddXRefactoringProvider))]
public class AddXRefactoringProvider: CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var refactoringContext = await TryGetValidSyntaxToSuggestRefactoringAsync(context);
        if (refactoringContext == null)
            return;

        var registrationMethod = await FindNearestRegistrationMethodAsync(context);
        if (registrationMethod?.Body != null)
        {
            var provider = new CodeActionProvider(context);
            
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            if (semanticModel == null)
                return;

            if (ModelExtensions.GetDeclaredSymbol(semanticModel, refactoringContext.TypeToRegister) is not INamedTypeSymbol typeSymbol)
                return;
            
            var typeNamespaceSymbol = typeSymbol.ContainingNamespace;
            INamespaceSymbol? baseNamespaceSymbol = null;
            if (refactoringContext.SelectedBaseType is {Type: NameSyntax name})
            {
                if (typeSymbol.BaseType?.Name == name.ToString())
                    baseNamespaceSymbol = typeSymbol.BaseType?.ContainingNamespace;

                if (typeSymbol.Interfaces.FirstOrDefault(i => i.Name == name.ToString()) is { } symbol)
                    baseNamespaceSymbol = symbol.ContainingNamespace;;
            }

            var requiredUsings = UsingsProvider.GetUsings(typeNamespaceSymbol, baseNamespaceSymbol);
            
            var codeActions = provider.PrepareCodeActions(refactoringContext, registrationMethod, requiredUsings);
            foreach (var codeAction in codeActions)
            {
                context.RegisterRefactoring(codeAction);
            }
        }
    }

    private static async Task<MethodDeclarationSyntax?> FindNearestRegistrationMethodAsync(CodeRefactoringContext context)
    {
        var docPath = context.Document.FilePath;
        var docDir = Path.GetDirectoryName(docPath);

        if (docDir == null) 
            return null;
        
        var method =
            await ScanFolderForRegistrationMethodAsync(docDir, context, context.CancellationToken);

        return method;
    }

    private static async Task<MethodDeclarationSyntax?> ScanFolderForRegistrationMethodAsync(string folders, CodeRefactoringContext context, CancellationToken token)
    {
        var docs = context.Document.Project.Documents.Where(d =>
        {
            var dirs = Path.GetDirectoryName(d.FilePath);
            return dirs != null && folders.Contains(dirs) && context.Document.Id != d.Id;
        });
        
        //sorting by longest file path, longer path => closer file is to document which had triggered refactoring
        foreach (var doc in docs.OrderByDescending(d => d.FilePath?.Length))
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

    private static async Task<RefactoringContext?> TryGetValidSyntaxToSuggestRefactoringAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var triggerToken = root?.FindToken(context.Span.Start);
        var node = triggerToken?.Parent;
        TypeDeclarationSyntax declaration;
        if (node is TypeDeclarationSyntax decl && node is not InterfaceDeclarationSyntax)
            declaration = decl;
        // else if (node?.Parent is TypeDeclarationSyntax decl2)
        //     declaration = decl2;
        else
            return null;
        
        //if (triggerToken is BaseTypeSyntax bt)
        
        //generics are harder, maybe later
        if (declaration.Arity != 0 || declaration.Modifiers.IndexOf(SyntaxKind.StaticKeyword) >= 0)
            return null;

        var bt = triggerToken?.Parent as BaseTypeSyntax;
        if (bt != null)
        {
            return new RefactoringContext(declaration, bt);
        }
        
        return new RefactoringContext(declaration, null);

    }
}