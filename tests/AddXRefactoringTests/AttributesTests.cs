using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using RoslynTestKit;

namespace AddXRefactoringTests;

public class AttributeTests: CodeRefactoringTestFixture
{
    [Test]
    public async Task DontRegisterIfIgnoreAttributeAdded()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    [|public class Bar|] 
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
        [IgnoreRegistrationMethod]
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services;
        }
    }
}
            ");
        
        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new [] {"Top", "Nested"});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, new [] {"Top"});
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsTrue(array.IsEmpty);
    }
    
    [Test]
    public async Task RegisterIfAttributeAdded()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    [|public class Bar|] 
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
        [RegistrationMethod]
        public void RegisterServices(IServiceCollection foo)
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
        [RegistrationMethod]
        public void RegisterServices(IServiceCollection foo)
        {
         foo.AddScoped<Bar>();
        }
    }
}
            ";
        
        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new [] {"Top", "Nested"});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, new [] {"Top"});
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.AreEqual(3, array.Length);
        
        Verify.CodeAction(array[1], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task RegisterWithInterface()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                namespace Lib 
                {
                    public class [|Foo|]: IFoo, IBar
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
            return services;
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
            services.AddScoped<IFoo, Foo>();
            return services;
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new [] {"Top", "Nested"});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, new [] {"Top"});
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.AreEqual(3, array.Length);
        
        Verify.CodeAction(array[1], registrationDoc, expectedRegistrationMethod);
    }
    
    protected override string LanguageName => LanguageNames.CSharp;
    protected override CodeRefactoringProvider CreateProvider()
    {
        return new AddXRefactoringProvider.AddXRefactoringProvider();
    }
}