![Nuget](https://img.shields.io/nuget/v/MicrosoftDI.AddXRefactoring)
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
* [x] First base type in your class declaration will be used as a first type argument in extension method call.
* [x] Code action will add registration onto first line of nearest registration method with separate statement `services.AddX<IFoo, Foo>();`.
* [x] You can annotate method which follows convention, but you don't want to be considered as RegistrationMethod with `[IgnoreRegistrationMethod]` attribute.
* [x] You can annotate method which does not follow convention, but you want it to be considered as RegistrationMethod with `[RegistrationMethod]` attribute.
* [x] Add required using if it is not yet added
* [x] Code action will add registration into `return services.AddX<Foo>().AddX<Bar>()` invocation chain.
* [ ] You can trigger refactoring on a specific item in your class' base list, and it will register method as such base.
