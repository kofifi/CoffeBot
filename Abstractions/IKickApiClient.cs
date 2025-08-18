using System.Net.Http;

namespace CoffeBot.Abstractions;

public interface IKickApiClient
{
    Task<(int Status, string Body)> SendAsync(
        HttpMethod method,
        string path,
        string? accessToken,
        HttpContent? content,
        CancellationToken ct = default);
}

