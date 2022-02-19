using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

/// <summary>
/// Refactoring provider - acts as an entry point.
/// Also responsible for collecting info about context where refactoring was triggered, so it can be passed to code action provider.
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AddXRefactoringProvider))]
public class AddXRefactoringProvider: CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var refactoringContext = await TryGetValidSyntaxToSuggestRefactoringAsync(context);
        if (refactoringContext == null)
            return;

        var registrationMethod = await FindNearestRegistrationMethodAsync(context);
        if (registrationMethod?.Body == null)
            return;
        
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (semanticModel == null)
            return;

        if (ModelExtensions.GetDeclaredSymbol(semanticModel, refactoringContext.TypeToRegister) is not INamedTypeSymbol typeSymbol)
            return;
        
        var requiredUsings = GetRequiredUsings(typeSymbol, refactoringContext);

        var provider = new CodeActionProvider(context);
        var codeActions = provider.PrepareCodeActions(refactoringContext, registrationMethod, requiredUsings);
        foreach (var codeAction in codeActions)
        {
            context.RegisterRefactoring(codeAction);
        }
    }

    private static ISet<UsingDirectiveSyntax> GetRequiredUsings(INamedTypeSymbol typeSymbol, RefactoringContext refactoringContext)
    {
        var typeNamespaceSymbol = typeSymbol.ContainingNamespace;
        INamespaceSymbol? baseNamespaceSymbol = null;
        if (refactoringContext.SelectedBaseType is {Type: NameSyntax name})
        {
            if (typeSymbol.BaseType?.Name == name.ToString())
                baseNamespaceSymbol = typeSymbol.BaseType?.ContainingNamespace;

            if (typeSymbol.Interfaces.FirstOrDefault(i => i.Name == name.ToString()) is { } symbol)
                baseNamespaceSymbol = symbol.ContainingNamespace;
        }

        var requiredUsings = UsingsProvider.GetUsings(typeNamespaceSymbol, baseNamespaceSymbol);
        return requiredUsings;
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
        var docs = GetDocs(context, folders);
        
        //sorting by longest dir count. Longer dir chain => closer file is to document which had triggered refactoring
        foreach (var (_, doc) in docs.OrderByDescending(tup => tup.Length))
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

    private static IEnumerable<(int Length, Document doc)> GetDocs(CodeRefactoringContext context, string folders)
    {
        foreach (var doc in context.Document.Project.Documents)
        {
            var dirs = Path.GetDirectoryName(doc.FilePath); //for some reason d.Folders does not work, maybe it's Rider fault
            if (dirs != null && folders.Contains(dirs) && context.Document.Id != doc.Id)
                yield return (dirs.Split(Path.DirectorySeparatorChar).Length, doc);
        }
    }

    private static MethodDeclarationSyntax? TryGetConfigureServices(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (node is not ClassDeclarationSyntax {Identifier.ValueText: "Startup"})
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

        var walker = new TriggerLocationSyntaxWalker();
        
        var triggerToken = root?.FindToken(context.Span.Start);
        var node = triggerToken?.Parent;
        walker.Visit(node);

        if (walker.TypeDeclarationSyntax == null)
            return null;
        
        //todo: generics are more complicated, maybe later
        if (walker.TypeDeclarationSyntax.Arity != 0 || walker.TypeDeclarationSyntax.Modifiers.IndexOf(SyntaxKind.StaticKeyword) >= 0)
            return null;

        return new RefactoringContext(walker.TypeDeclarationSyntax, walker.BaseType);
    }
}