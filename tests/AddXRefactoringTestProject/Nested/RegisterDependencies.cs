using Microsoft.Extensions.DependencyInjection;

namespace AddXRefactoringTestProject.Nested;

public static class RegisterDependencies
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        services.AddSingleton<Class1>();
        return services;
    }
}