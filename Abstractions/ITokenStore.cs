namespace CoffeBot.Abstractions;

public interface ITokenStore
{
    void Save(HttpContext ctx, string accessToken, string? refreshToken);
    (string? AccessToken, string? RefreshToken) Read(HttpContext ctx);
    void Clear(HttpContext ctx);
}