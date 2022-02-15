using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using MicrosoftDI.AddXRefactoring.Provider;
using RoslynTestKit;

namespace AddXRefactoringTests;

public partial class Tests: CodeRefactoringTestFixture
{
    protected override string LanguageName => LanguageNames.CSharp;
    protected override CodeRefactoringProvider CreateProvider()
    {
        return new AddXRefactoringProvider();
    }
}