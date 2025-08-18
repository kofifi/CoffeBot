using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>
    /// Subscribes to chat message events for the broadcaster (channel) via webhook.
    /// Requires the access token to have 'events:subscribe' scope.
    /// </summary>
    public async Task SubscribeToChatAsync(
        string accessToken,
        int channelId,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_kick.ApiBase.TrimEnd('/')}/public/v1/events/subscriptions");

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("Client-Id", _kick.ClientId);

        var body = new
        {
            events = new[]
            {
                new { name = "chat.message.sent", version = 1 } // add more events here if needed
            },
            method = "webhook",
            broadcaster_user_id = channelId
        };

        req.Content = JsonContent.Create(body);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Subscribe failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {text}");
        }
    }

    // ------- Optional helpers (can add to your interface if you like) -------

    public async Task<string> ListSubscriptionsAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_kick.ApiBase.TrimEnd('/')}/public/v1/events/subscriptions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("Client-Id", _kick.ClientId);

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        return text; // or deserialize to a record
    }

    public async Task DeleteSubscriptionsAsync(string accessToken, CancellationToken ct, params string[] ids)
    {
        if (ids is null || ids.Length == 0) return;

        var query = string.Join("&", ids.Select(id => $"id={Uri.EscapeDataString(id)}"));
        var url = $"{_kick.ApiBase.TrimEnd('/')}/public/v1/events/subscriptions?{query}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("Client-Id", _kick.ClientId);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Delete failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {text}");
        }
    }
}
