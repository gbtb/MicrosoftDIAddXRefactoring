[![Nuget](https://img.shields.io/nuget/v/MicrosoftDI.AddXRefactoring)](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/)
# Microsoft DI AddX Refactoring Provider

This project provides Roslyn code refactoring, which automatically adds Add(Singleton|Scoped|Transient) from your new class declaration into nearest DI registration point.
It could be default ConfigureServices in Startup.cs, or your custom extension method analogous to it.

## Prerequisites
1. You use Microsoft.Extensions.DependencyInjection to register services
2. You use default `ConfigureServices` in Startup.cs and/or custom extension methods to register services with following signature: 
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

## Installation

It can be installed in any project through [nuget package](https://www.nuget.org/packages/MicrosoftDI.AddXRefactoring/)

## Features

* [x] Code Actions with AddSingleton|AddScoped|AddTransient methods with appropriate type parameters will be inferred from your class declaration.
* [x] Exact signature of Add[X] will be inferred from position, where refactoring was triggered:
  * If it was triggered on class name itself, then refactoring will register it as `AddX<ClassName>`
  * If it was triggered on a base type or an interface of a class, then refactoring will register it as `Add[X]<BaseType, ClassName>`
* [x] Refactoring will add registration into nearest RegistrationMethod using one of two heuristics:
  * If RegistrationMethod body is empty or it does not contains chain calls with length greater than 1, then registration will be added onto first line  with separate statement `services.AddX<IFoo, Foo>();`
  * If RegistrationMethod contains call chain of length greater than 1, then registration will be appended to that call chain
* [x] You can annotate method which follows convention, but you don't want to be considered as RegistrationMethod with `[IgnoreRegistrationMethod]` attribute.
* [x] You can annotate method which does not follow convention, but you want it to be considered as RegistrationMethod with `[RegistrationMethod]` attribute.
* [x] Adds missing using to RegistrationMethod
