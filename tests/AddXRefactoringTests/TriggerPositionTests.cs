using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using RoslynTestKit;

namespace AddXRefactoringTests;

[TestFixture]
public partial class Tests
{
    [Test]
    public async Task TriggerOnClass_ShouldRegisterOnlyImplType()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;

                namespace Space 
                {
                    public interface IFoo {}
                }

                namespace Space.Station
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    [|public class Bar|]: Foo, IFoo 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Space.Station
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
namespace Space.Station
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddScoped<Bar>();
            return services;
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Top/Nested/Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Top/Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        //Assert.AreEqual(4, array.Length);
        
        Verify.CodeAction(array[1], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task TriggerOnBaseClass_ShouldRegisterImplTypeAsBase()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;

                namespace Space 
                {
                    public interface IFoo {}
                }

                namespace Space.Station
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public [|class Bar|]: Foo, IFoo 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Space.Station
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
namespace Space.Station
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<Foo, Bar>();
            return services;
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Top/Nested/Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Top/Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.AreEqual(4, array.Length);
        
        Verify.CodeAction(array[0], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task TriggerOnImplInterface_ShouldRegisterImplTypeAsInterface()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

        var sourceText = @"
                using System;

                namespace Space 
                {
                    public interface IFoo {}
                }

                namespace Space.Station
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar: Foo, [|IFoo|] 
                    {
                       public int Prop { get; set; }
                    }
                }
            ";
        
        var registrationMethod = SourceText.From(@"
using System;
namespace Space.Station
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
namespace Space.Station
{
    public static class Registrator 
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddTransient<IFoo, Bar>();
            return services;
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), filePath: "Top/Nested/Lib.cs");
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, filePath: "Top/Registrator.cs");
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.IsFalse(array.IsEmpty);
        Assert.AreEqual(4, array.Length);
        
        Verify.CodeAction(array[2], registrationDoc, expectedRegistrationMethod);
    }
}