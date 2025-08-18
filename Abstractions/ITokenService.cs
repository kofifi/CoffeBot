using CoffeBot.Models;

namespace CoffeBot.Abstractions;

public interface ITokenService
{
    Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
    Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, string hintType, CancellationToken ct);
}