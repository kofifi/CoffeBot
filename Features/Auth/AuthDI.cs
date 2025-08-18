using CoffeBot.Abstractions;
using CoffeBot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeBot.Features.Auth;

public static class AuthDI
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddSingleton<IPkceService, PkceService>();
        services.AddSingleton<IStateService, StateService>();
        services.AddSingleton<IAuthUrlBuilder, AuthUrlBuilder>();
        return services;
    }
}
