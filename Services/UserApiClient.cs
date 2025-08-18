// Services/UserApiClient.cs
using System.Net.Http.Headers;
using CoffeBot.Abstractions;
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
}