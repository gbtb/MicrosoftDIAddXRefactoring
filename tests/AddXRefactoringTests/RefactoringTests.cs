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

public class Tests: CodeRefactoringTestFixture
{
    [Test]
    public async Task RegisterInSameFolder()
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
            services.AddSingleton<Foo>();
            return services;
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new string[] {});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, new string[] {});
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
    public async Task RegisterInUpperFolder()
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
            services.AddScoped<Bar>();
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
    
    [Test]
    public async Task RegisterInConfigureServices()
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
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddDbContext<ApplicationDbContext>(options =>
            //    options.UseSqlServer(Configuration.GetConnectionString(""DefaultConnection"")));
            services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(Configuration.GetConnectionString(""SQLite"")));
        }
    }
}
            ");
        
        var expectedRegistrationMethod = @"
using System;
namespace Lib 
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddDbContext<ApplicationDbContext>(options =>
            //    options.UseSqlServer(Configuration.GetConnectionString(""DefaultConnection"")));
            services.AddTransient<Bar>();
            //services.AddDbContext<ApplicationDbContext>(options =>
            //    options.UseSqlServer(Configuration.GetConnectionString(""DefaultConnection"")));
            services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(Configuration.GetConnectionString(""SQLite"")));
        }
    }
}
            ";

        var markup = new CodeMarkup(sourceText);
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new [] {"Top", "Nested"});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Startup.cs", registrationMethod, new string[] {});
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.That(array.IsEmpty, Is.False);
        Assert.That(array.Length, Is.EqualTo(3));
        Assert.That(array[0].Title, Is.EqualTo("Register with AddSingleton<Bar>"));
        Assert.That(array[1].Title, Is.EqualTo("Register with AddScoped<Bar>"));
        Assert.That(array[2].Title, Is.EqualTo("Register with AddTransient<Bar>"));
        
        Verify.CodeAction(array[2], registrationDoc, expectedRegistrationMethod);
    }
    
    [Test]
    public async Task DontRegisterInLowerFolder()
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
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            return services;
        }
    }
}
            ");
        
        

        var markup = new CodeMarkup(sourceText);

        var doc = libProject.AddDocument("Lib.cs", SourceText.From(markup.Code), new [] {"Top", "Lib"});
        libProject = doc.Project;
        var registrationDoc = libProject.AddDocument("Registrator.cs", registrationMethod, new [] {"Top", "Nested"});
        libProject = registrationDoc.Project;

        var builder = ImmutableArray.CreateBuilder<CodeAction>();
        doc = libProject.GetDocument(doc.Id);
        
        var context = new CodeRefactoringContext(doc, markup.Locator.GetSpan(), a => builder.Add(a), CancellationToken.None);
        await CreateProvider().ComputeRefactoringsAsync(context);
        var array = builder.ToImmutable();
        
        Assert.AreEqual(2, libProject.Documents.Count());
        Assert.IsTrue(array.IsEmpty);
    }

    private static IEnumerable<TestCaseData> SourcesWithWrongLocation = new[]
    {
        new TestCaseData(@"
                using System;
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar
                    {
                       [|public int Prop { get; set; }|]
                    }
                }
            ").SetName("Property"),
        new TestCaseData(@"
                using System;
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar
                    {
                       public int [|Prop|] { get; set; }
                    }
                }
            ").SetName("PropertyName"),
        new TestCaseData(@"
                using System;
                [|namespace Lib|] 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar
                    {
                       public int [|Prop|] { get; set; }
                    }
                }
            ").SetName("Namespace decl"),
        new TestCaseData(@"
                [|using System;|]
                namespace Lib
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }

                    public class Bar
                    {
                       public int [|Prop|] { get; set; }
                    }
                }
            ").SetName("Using")
    };

    [Test]
    [TestCaseSource(nameof(SourcesWithWrongLocation))]
    public async Task ShouldRegisterOnlyOnClassDecl(string sourceText)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));

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
        
        Assert.That(array.IsEmpty, Is.True);
        
    }
    
    protected override string LanguageName => LanguageNames.CSharp;
    protected override CodeRefactoringProvider CreateProvider()
    {
        return new AddXRefactoringProvider();
    }
}