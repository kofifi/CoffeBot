using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeBot.Abstractions;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class EventSubClient : IEventSubClient
{
    private readonly HttpClient _http;
    private readonly KickOptions _kick;

    public EventSubClient(IHttpClientFactory factory, IOptions<KickOptions> kick)
    {
        _http = factory.CreateClient("kick-api");
        _kick = kick.Value;
    }

    public async Task SubscribeToChatAsync(
        string accessToken,
        int channelId,
        string callbackUrl,
        string secret,
        CancellationToken ct)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"{_kick.ApiBase.TrimEnd('/')}/public/v1/events/subscribe";
        var payload = new
        {
            type = "channel.chat.message",
            broadcaster_user_id = channelId,
            callback = callbackUrl,
            secret
        };

        using var resp = await _http.PostAsJsonAsync(url, payload, ct);
        resp.EnsureSuccessStatusCode();
    }
}
