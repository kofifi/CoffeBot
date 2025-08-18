namespace CoffeBot.Abstractions;

public interface IEventSubClient
{
    Task SubscribeToChatAsync(
        string accessToken,
        int channelId,
        CancellationToken ct);
}
