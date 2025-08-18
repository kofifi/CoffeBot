using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class KickWebhookVerifier
{
    private readonly HttpClient _http;
    private readonly KickOptions _kick;
    private RSA? _publicKey;

    public KickWebhookVerifier(IHttpClientFactory factory, IOptions<KickOptions> kick)
    {
        _http = factory.CreateClient("kick-api");
        _kick = kick.Value;
    }

    private async Task<RSA> GetPublicKeyAsync(CancellationToken ct)
    {
        if (_publicKey is not null) return _publicKey;
        var pem = await _http.GetStringAsync($"{_kick.ApiBase.TrimEnd('/')}/public/v1/public-key", ct);
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        _publicKey = rsa;
        return rsa;
    }

    public async Task<bool> VerifyAsync(HttpRequest req, string body, CancellationToken ct = default)
    {
        if (!req.Headers.TryGetValue("Kick-Event-Signature", out var sigValues)) return false;
        if (!req.Headers.TryGetValue("Kick-Event-Message-Id", out var idValues)) return false;
        if (!req.Headers.TryGetValue("Kick-Event-Message-Timestamp", out var tsValues)) return false;

        var payload = $"{idValues}.{tsValues}.{body}";
        var rsa = await GetPublicKeyAsync(ct);

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(sigValues.ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
