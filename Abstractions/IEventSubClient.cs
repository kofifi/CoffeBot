using System.Threading;
using System.Threading.Tasks;

namespace CoffeBot.Abstractions;

public interface IEventSubClient
{
    Task SubscribeToChatAsync(string accessToken, int channelId, CancellationToken ct);
    Task<string> ListSubscriptionsAsync(string accessToken, CancellationToken ct);
    Task DeleteSubscriptionsAsync(string accessToken, CancellationToken ct, params string[] ids);
}