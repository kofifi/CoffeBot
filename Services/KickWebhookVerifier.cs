using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoffeBot.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

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

    private static RSA ParsePublicKey(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException("Klucz publiczny jest pusty!");

        if (!pem.Contains("-----BEGIN PUBLIC KEY-----") || !pem.Contains("-----END PUBLIC KEY-----"))
            throw new InvalidOperationException($"Nieprawidłowy format PEM klucza publicznego:\n{pem}");

        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Błąd podczas importu PEM: {ex.Message}\nPEM:\n{pem}", ex);
        }
    }

    private async Task<RSA> GetPublicKeyAsync(CancellationToken ct)
    {
        if (_publicKey is not null) return _publicKey;

        var content = await _http.GetStringAsync($"{_kick.ApiBase.TrimEnd('/')}/public/v1/public-key", ct);
        Console.WriteLine("Kick public key response:\n" + content);

        string pem = null!;
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataEl) &&
                dataEl.TryGetProperty("public_key", out var pk))
            {
                pem = pk.GetString() ?? throw new InvalidOperationException("Brak klucza publicznego w odpowiedzi.");
            }
            else if (root.TryGetProperty("public_key", out var pk2) ||
                     root.TryGetProperty("publicKey", out pk2))
            {
                pem = pk2.GetString() ?? throw new InvalidOperationException("Brak klucza publicznego w odpowiedzi.");
            }
            else
            {
                throw new InvalidOperationException("Brak klucza publicznego w odpowiedzi.");
            }
        }
        catch (JsonException)
        {
            // Jeśli nie jest to JSON, sprawdź czy to czysty PEM
            if (content.Contains("-----BEGIN PUBLIC KEY-----"))
                pem = content.Trim();
            else
                throw new InvalidOperationException("Nieprawidłowy format odpowiedzi z Kick API:\n" + content);
        }

        _publicKey = ParsePublicKey(pem);
        return _publicKey;
    }

    public async Task<bool> VerifyAsync(HttpRequest req, string body, CancellationToken ct = default)
    {
        if (!req.Headers.TryGetValue("Kick-Event-Signature", out var sigValues)) return false;
        if (!req.Headers.TryGetValue("Kick-Event-Message-Id", out var idValues)) return false;
        if (!req.Headers.TryGetValue("Kick-Event-Message-Timestamp", out var tsValues)) return false;

        var payload = $"{idValues.FirstOrDefault()}.{tsValues.FirstOrDefault()}.{body}";
        var rsa = await GetPublicKeyAsync(ct);

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(sigValues.FirstOrDefault() ?? string.Empty);
        }
        catch (FormatException)
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        try
        {
            return rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}