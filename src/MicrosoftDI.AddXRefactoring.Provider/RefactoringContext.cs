using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

public class RefactoringContext
{
    public RefactoringContext(TypeDeclarationSyntax typeToRegister, BaseTypeSyntax? selectedBaseType)
    {
        TypeToRegister = typeToRegister;
        SelectedBaseType = selectedBaseType;
    }

    public TypeDeclarationSyntax TypeToRegister { get; }
    
    public BaseTypeSyntax? SelectedBaseType { get; }
}