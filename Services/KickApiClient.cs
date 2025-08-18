using System.Net.Http;
using System.Net.Http.Headers;
using CoffeBot.Abstractions;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class KickApiClient : IKickApiClient
{
    private readonly HttpClient _http;
    private readonly KickOptions _opt;

    public KickApiClient(IHttpClientFactory factory, IOptions<KickOptions> opt)
    {
        _http = factory.CreateClient("kick-api");
        _opt = opt.Value;
    }

    public async Task<(int Status, string Body)> SendAsync(
        HttpMethod method,
        string path,
        string? accessToken,
        HttpContent? content,
        CancellationToken ct = default)
    {
        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{_opt.ApiBase.TrimEnd('/')}/{path.TrimStart('/')}";

        using var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(accessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (content is not null)
            req.Content = content;

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return ((int)resp.StatusCode, body);
    }
}

