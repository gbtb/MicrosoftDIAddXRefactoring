[![Nuget](https://img.shields.io/nuget/v/MicrosoftDI.AddXRefactoring)](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/)
[![codecov](https://codecov.io/gh/gbtb/MicrosoftDIAddXRefactoring/branch/master/graph/badge.svg?token=SP9HHTRPE7)](https://codecov.io/gh/gbtb/MicrosoftDIAddXRefactoring)
# Microsoft DI AddX Refactoring Provider

This project provides a Roslyn code refactoring, which automatically adds call to Add(Singleton|Scoped|Transient) inferred from your class declaration into a nearest DI registration method.
It could be a default ConfigureServices in Startup.cs, or your custom extension method.

![Screenshot_20220219_203218](https://user-images.githubusercontent.com/37017396/154797213-9a36e4a2-f20e-4835-8c5c-e5f3e513e0be.png)
![Screenshot_20220219_202805](https://user-images.githubusercontent.com/37017396/154797216-10ed2a02-7775-4a80-8634-45f5806eee1c.png)

## Assumptions made
1. You use Microsoft.Extensions.DependencyInjection to register services
2. You use default `ConfigureServices` in Startup.cs and/or custom extension methods (Registration Method) to register services, with the following signature: 
```{c#}
public static class Module 
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        return services.AddSingleton<Foo>()
            ...
            ;
    }
}
```
3. You want to modularize and split your registration method into multiple files, and keep them closer to the classes which they register OR you just want to reduce the number of keystrokes you need to register classes in a DI container
4. You don't want to use tools like [Scrutor](https://github.com/khellang/Scrutor) for an automated assembly scanning
5. Then this code refactoring can make your life a little bit easier, generating a registration method call for you 🙂

## Features

* [x] Code Actions with AddSingleton|AddScoped|AddTransient methods with appropriate type parameters will be inferred from your class declaration
* [x] Exact signature of an AddX will be inferred from position, where the refactoring was triggered:
  * If it was triggered on a class name itself, then the refactoring will register it as `AddX<ClassName>`
  * If it was triggered on a base type or an interface of a class, then the refactoring will register it as `AddX<BaseType, ClassName>`
* [x] Refactoring will add a registration into a nearest registration method using one of the two heuristics:
  * If a registration method body is empty or it does not contain chain calls with a length greater than 1, then a registration will be added to the first line with a separate statement `services.AddX<IFoo, Foo>();`
  * If a Registration Method contains call chain of length greater than 1, then a registration will be appended to that call chain
* [x] You can annotate a method which follows the convention, but you don't want to be considered as a registration method with a `[IgnoreRegistrationMethod]` attribute
* [x] You can annotate a method which does not follow the convention, but you want it to be considered as a registration method with a  `[RegistrationMethod]` attribute
* [x] Refactoring automatically adds usings to a registration method's file, if it's required

## Installation

* This refactoring can be installed in any project through the [nuget package](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/) . It works with Rider 2021.3 and Visual Studio 2022.
* To install it in all projects for a given solution, create Directory.Build.props file with following content:
```
<Project>
    <ItemGroup>
        <PackageReference Include="MicrosoftDI.AddXRefactoring" Version="{CurrentVersion}">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
        </PackageReference>
    </ItemGroup>
</Project>
```

## Contributing

Feel free to use Github issues for questions or feature requests 
