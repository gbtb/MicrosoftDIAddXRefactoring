using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using MicrosoftDI.AddXRefactoring.Provider;
using NUnit.Framework;
using RoslynTestKit;

namespace AddXRefactoringTests;

public class InvocationTests: CodeRefactoringTestFixture
{
    [Test]
    public async Task RegisterInSameFolder_SeparateStatements()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    [|public class Foo|]
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services.AddSingleton<Test>();
        }
    }
}
            ");
        
        var expectedRegistrationMethod = @"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<Foo>();
            return services.AddSingleton<Test>();
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.AreEqual(3, array.Length);
        
        Verify.CodeAction(array[0], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task RegisterInSameFolder_InvocationChain()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    [|public class Foo|]: IFoo, IBar
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services.AddSingleton<Test1>()
                .AddScoped<ITest2, Test2>();
        }
    }
}
            ");
        
        var expectedRegistrationMethod = @"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services.AddSingleton<Test1>()
                .AddScoped<ITest2, Test2>()
                .AddTransient<Foo>();
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.That(array.Length, Is.EqualTo(3));
        
        Verify.CodeAction(array[2], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task EmptyRegistrationMethod()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    [|public class Foo|]: IFoo, IBar
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
           
        }
    }
}
            ");
        
        var expectedRegistrationMethod = @"
using System;
namespace Lib 
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services.AddTransient<Foo>();
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.That(array.Length, Is.EqualTo(3));
        
        Verify.CodeAction(array[2], registrationDoc, expectedRegistrationMethod);
    }
    
    protected override string LanguageName => LanguageNames.CSharp;
    protected override CodeRefactoringProvider CreateProvider()
    {
        return new AddXRefactoringProvider();
    }
}