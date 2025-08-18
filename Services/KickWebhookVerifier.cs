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
        if (!req.Headers.TryGetValue("X-Kick-Signature", out var sigValues)) return false;
        var received = sigValues.ToString().Trim();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(received));
    }
}
