using Microsoft.Extensions.DependencyInjection;

namespace CoffeBot.Features.Users;

public static class UsersDI
{
    public static IServiceCollection AddUsersFeature(this IServiceCollection services)
    {
        return services;
    }
}
