using CoffeBot.Abstractions;

namespace CoffeBot.Auth;

public sealed class SessionTokenStore : ITokenStore
{
    private const string AccessKey = "access_token";
    private const string RefreshKey = "refresh_token";

    public void Save(HttpContext ctx, string accessToken, string? refreshToken)
    {
        ctx.Session.SetString(AccessKey, accessToken);
        if (!string.IsNullOrEmpty(refreshToken))
            ctx.Session.SetString(RefreshKey, refreshToken);
    }

    public (string? AccessToken, string? RefreshToken) Read(HttpContext ctx)
        => (ctx.Session.GetString(AccessKey), ctx.Session.GetString(RefreshKey));

    public void Clear(HttpContext ctx)
    {
        ctx.Session.Remove(AccessKey);
        ctx.Session.Remove(RefreshKey);
    }
}