using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using MicrosoftDI.AddXRefactoring.Provider;
using NUnit.Framework;
using RoslynTestKit;

namespace AddXRefactoringTests;

[TestFixture]
public class FunctionalTests: CodeRefactoringTestFixture
{
    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        MSBuildLocator.RegisterDefaults();
    }
    
    [Test]
    public async Task Test()
    {
        var workspace = MSBuildWorkspace.Create();
        var project =
            await workspace.OpenProjectAsync("../../../../AddXRefactoringTestProject/AddXRefactoringTestProject.csproj");

        var doc = project.Documents.First(d => d.Name == "Class2.cs");
        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        
        var context = new CodeRefactoringContext(doc, TextSpan.FromBounds(52, 60), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        //Assert.That(project.Documents.Count(), Is.EqualTo(2)); /home/artem/Work/websales-git/Dns.Core
        Assert.IsTrue(array.IsEmpty);
    }
    
    [Test]
    [Ignore("Insert real file path")]
    public async Task TestWebsales()
    {
        var workspace = MSBuildWorkspace.Create();
        var project =
            await workspace.OpenProjectAsync("");

        var doc = project.Documents.First(d => d.Name == "AspNetMailSettings.cs");
        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        
        var context = new CodeRefactoringContext(doc, TextSpan.FromBounds(140, 160), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        //Assert.That(project.Documents.Count(), Is.EqualTo(2)); 
        Assert.IsTrue(array.IsEmpty);
    }

    protected override string LanguageName => LanguageNames.CSharp;
    protected override CodeRefactoringProvider CreateProvider()
    {
        return new AddXRefactoringProvider();
    }
}