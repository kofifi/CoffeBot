using System.Net.Http.Headers;
using System.Text;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class TokenService : ITokenService
{
    private readonly HttpClient _http;
    private readonly KickOptions _opt;

    public TokenService(IHttpClientFactory factory, IOptions<KickOptions> opt)
    {
        _http = factory.CreateClient("kick-auth");
        _opt = opt.Value;
    }

    public async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        var form = new Dictionary<string,string>
        {
            ["grant_type"]    = "authorization_code",
            ["client_id"]     = _opt.ClientId,
            ["redirect_uri"]  = _opt.RedirectUri,
            ["code_verifier"] = codeVerifier,
            ["code"]          = code
        };

        if (!string.IsNullOrEmpty(_opt.ClientSecret))
            form["client_secret"] = _opt.ClientSecret!;

        using var resp = await _http.PostAsync(_opt.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token exchange failed: {resp.StatusCode}\n{json}");

        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json)!;
    }

    public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var form = new Dictionary<string,string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = _opt.ClientId,
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrEmpty(_opt.ClientSecret))
            form["client_secret"] = _opt.ClientSecret!;

        using var resp = await _http.PostAsync(_opt.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json)!;
    }

    public async Task RevokeAsync(string token, string hintType, CancellationToken ct)
    {
        var url = $"{_opt.RevokeEndpoint}?token={Uri.EscapeDataString(token)}&token_hint_type={hintType}";
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string,string>()), ct);
        // Kick bywa „no-content”; brak sukcesu też nie musi być fatalny – brak throw
    }
}
