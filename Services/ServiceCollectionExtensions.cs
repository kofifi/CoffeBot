using CoffeBot.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeBot.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStore, SessionTokenStore>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserApiClient, UserApiClient>();
        return services;
    }
}
