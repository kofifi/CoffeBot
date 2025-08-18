// Services/UserApiClient.cs
using System.Net.Http.Headers;
using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class UserApiClient : IUserApiClient
{
    private readonly HttpClient _http;
    private readonly KickOptions _opt;

    public UserApiClient(IHttpClientFactory factory, IOptions<KickOptions> opt)
    {
        _http = factory.CreateClient("kick-api");
        _opt = opt.Value;
    }

    public async Task<(int Status, string Body)> GetCurrentRawAsync(string accessToken, CancellationToken ct)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.GetAsync(_opt.UsersEndpoint, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return ((int)resp.StatusCode, body);
    }

    public async Task<CurrentUserDto> GetCurrentAsync(string accessToken, CancellationToken ct)
    {
        var (_, body) = await GetCurrentRawAsync(accessToken, ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var first = root.GetProperty("data")[0];
        var id = first.TryGetProperty("user_id", out var idEl) ? idEl.GetInt32() : 0;
        string? username = null;
        if (first.TryGetProperty("username", out var nameEl))
            username = nameEl.GetString();
        return new CurrentUserDto
        {
            Id = id,
            Username = username,
            Raw = root.Clone()
        };
    }
}

