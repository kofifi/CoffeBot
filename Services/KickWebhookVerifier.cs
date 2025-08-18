using System.Security.Cryptography;
using System.Text;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class KickWebhookVerifier
{
    private readonly KickEventOptions _opt;

    public KickWebhookVerifier(IOptions<KickEventOptions> opt)
    {
        _opt = opt.Value;
    }

    public bool Verify(HttpRequest req, string body)
    {
        if (!_opt.WebhookSecret.Any()) return false;

        // Kick signs each webhook request using the event id, timestamp and body
        if (!req.Headers.TryGetValue("X-Kick-Signature", out var sigValues)) return false;
        if (!req.Headers.TryGetValue("X-Kick-Event-Id", out var idValues)) return false;
        if (!req.Headers.TryGetValue("X-Kick-Timestamp", out var tsValues)) return false;

        var payload = $"{idValues}{tsValues}{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        var received = sigValues.ToString().Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(received));
    }
}
