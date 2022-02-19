using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

/// <summary>
/// Used to pass data from detection phase to code action generation phase
/// </summary>
internal class RefactoringContext
{
    public RefactoringContext(TypeDeclarationSyntax typeToRegister, BaseTypeSyntax? selectedBaseType)
    {
        TypeToRegister = typeToRegister;
        SelectedBaseType = selectedBaseType;
    }

    /// <summary>
    /// Which type we should register
    /// </summary>
    public TypeDeclarationSyntax TypeToRegister { get; }
    
    /// <summary>
    /// Under which base type we should register
    /// </summary>
    public BaseTypeSyntax? SelectedBaseType { get; }
}