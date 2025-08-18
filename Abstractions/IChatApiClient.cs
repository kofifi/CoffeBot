namespace CoffeBot.Abstractions;

using CoffeBot.Models;

public interface IChatApiClient
{
    Task<ChatSendResult> SendAsync(
        string accessToken,
        ChatSendCommand cmd,
        CancellationToken ct);
}