namespace CoffeBot.Models;

public sealed record TokenResponse(
    string access_token,
    string token_type,
    int    expires_in,
    string scope,
    string? refresh_token
);