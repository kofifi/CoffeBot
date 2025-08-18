using CoffeBot.Abstractions;

namespace CoffeBot.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/");

        // /me (raw passthrough + refresh)
        group.MapGet("me", async (HttpContext ctx, ITokenStore store, ITokenService tokens, IUserApiClient api) =>
        {
            var (access, refresh) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            var (status, body) = await api.GetCurrentRawAsync(access, ctx.RequestAborted);
            if (status == 401 && !string.IsNullOrEmpty(refresh))
            {
                var refreshed = await tokens.RefreshAsync(refresh, ctx.RequestAborted);
                if (refreshed is not null)
                {
                    store.Save(ctx, refreshed.access_token, refreshed.refresh_token);
                    (status, body) = await api.GetCurrentRawAsync(refreshed.access_token, ctx.RequestAborted);
                }
            }

            return Results.Content(body, "application/json", statusCode: status);
        });

        // Opcjonalnie: /debug/introspect (jeśli dodałeś w Services/UserApiClient metodę do introspekcji)
        // group.MapGet("debug/introspect", ...);

        return app;
    }
}