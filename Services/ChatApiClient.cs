using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class ChatApiClient : IChatApiClient
{
    private readonly HttpClient _http;
    private readonly KickOptions _opt;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ChatApiClient(IHttpClientFactory factory, IOptions<KickOptions> opt)
    {
        _http = factory.CreateClient("kick-api");
        _opt = opt.Value;
    }

    public async Task<ChatSendResult> SendAsync(string accessToken, ChatSendCommand cmd, CancellationToken ct)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"{_opt.ApiBase.TrimEnd('/')}/public/v1/chat";

        // Build request per docs (omit nulls)
        var payload = new Dictionary<string, object?>
        {
            ["content"] = cmd.Content,
            ["type"] = cmd.Type
        };
        if (cmd.BroadcasterUserId is int uid) payload["broadcaster_user_id"] = uid;
        if (!string.IsNullOrWhiteSpace(cmd.ReplyToMessageId)) payload["reply_to_message_id"] = cmd.ReplyToMessageId;

        using var body = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, body, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            // throw with body for easier debugging
            throw new HttpRequestException($"Kick Chat POST failed {(int)resp.StatusCode}: {json}");
        }

        // success shape: { "data": { "is_sent": true, "message_id": "..." }, "message": "..." }
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        return new ChatSendResult(
            data.GetProperty("is_sent").GetBoolean(),
            data.GetProperty("message_id").GetString() ?? string.Empty
        );
    }
}
