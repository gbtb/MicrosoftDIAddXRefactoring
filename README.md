[![Nuget](https://img.shields.io/nuget/v/MicrosoftDI.AddXRefactoring)](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/)
[![codecov](https://codecov.io/gh/gbtb/MicrosoftDIAddXRefactoring/branch/master/graph/badge.svg?token=SP9HHTRPE7)](https://codecov.io/gh/gbtb/MicrosoftDIAddXRefactoring)
# Microsoft DI AddX Refactoring Provider

This project provides Roslyn code refactoring, which automatically adds Add(Singleton|Scoped|Transient) inferred from your class declaration into nearest DI registration method.
It could be default ConfigureServices in Startup.cs, or your custom extension method to it.

![Screenshot_20220219_203218](https://user-images.githubusercontent.com/37017396/154797213-9a36e4a2-f20e-4835-8c5c-e5f3e513e0be.png)
![Screenshot_20220219_202805](https://user-images.githubusercontent.com/37017396/154797216-10ed2a02-7775-4a80-8634-45f5806eee1c.png)

## Assumptions made
1. You use Microsoft.Extensions.DependencyInjection to register services
2. You use default `ConfigureServices` in Startup.cs and/or custom extension methods (Registration Method) to register services with following signature: 
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
3. You want to modularize and split your registration method into multiple files, and keep them closer to classes which they register OR you just want to reduce number of keystrokes you need to register class in DI container
4. You don't want to use tools like [Scrutor](https://github.com/khellang/Scrutor) for automated assembly scanning
5. Then this code refactoring can make your life a little bit easier, generating registration method call for you 🙂

## Features

* [x] Code Actions with AddSingleton|AddScoped|AddTransient methods with appropriate type parameters will be inferred from your class declaration
* [x] Exact signature of Add[X] will be inferred from position, where refactoring was triggered:
  * If it was triggered on class name itself, then refactoring will register it as `AddX<ClassName>`
  * If it was triggered on a base type or an interface of a class, then refactoring will register it as `Add[X]<BaseType, ClassName>`
* [x] Refactoring will add registration into nearest RegistrationMethod using one of two heuristics:
  * If RegistrationMethod body is empty or it does not contains chain calls with length greater than 1, then registration will be added onto first line  with separate statement `services.AddX<IFoo, Foo>();`
  * If RegistrationMethod contains call chain of length greater than 1, then registration will be appended to that call chain
* [x] You can annotate method which follows convention, but you don't want to be considered as RegistrationMethod with `[IgnoreRegistrationMethod]` attribute
* [x] You can annotate method which does not follow convention, but you want it to be considered as RegistrationMethod with `[RegistrationMethod]` attribute
* [x] Adds missing using to RegistrationMethod

## Installation

* It can be installed in any project through [nuget package](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/)
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

Feel free to create Github issue for questions or feature requests 
