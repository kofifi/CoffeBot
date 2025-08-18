using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CoffeBot.Http;

public static class HttpClientsRegistration
{
    public static IServiceCollection AddKickHttpClients(this IServiceCollection services)
    {
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, i => TimeSpan.FromMilliseconds(200 * i));

        services.AddHttpClient("kick-auth").AddPolicyHandler(retry);
        services.AddHttpClient("kick-api").AddPolicyHandler(retry);

        return services;
    }
}