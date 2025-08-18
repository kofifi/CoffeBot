namespace CoffeBot.Abstractions;

public interface IEventSubClient
{
    Task SubscribeToChatAsync(
        string accessToken,
        int channelId,
        string callbackUrl,
        string secret,
        CancellationToken ct);
}
