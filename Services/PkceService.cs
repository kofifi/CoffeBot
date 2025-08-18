using System.Security.Cryptography;
using System.Text;
using CoffeBot.Abstractions;

namespace CoffeBot.Services;

public sealed class PkceService : IPkceService
{
    public (string CodeVerifier, string CodeChallenge) CreatePair()
    {
        var verifier = CreateRandomBase64Url(32); // 43–128 chars after base64url
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        var challenge = ToBase64Url(hash);
        return (verifier, challenge);
    }

    public string CreateRandomBase64Url(int bytesLength = 32)
    {
        var bytes = new byte[bytesLength];
        RandomNumberGenerator.Fill(bytes);
        return ToBase64Url(bytes);
    }

    private static string ToBase64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}