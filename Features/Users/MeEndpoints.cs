using CoffeBot.Abstractions;

namespace CoffeBot.Features.Users;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/users");
        g.MapGet("/me", MeHandler);
        return app;
    }

    private static async Task<IResult> MeHandler(
        HttpContext ctx,
        ITokenStore store,
        ITokenService tokens,
        IUserApiClient api)
    {
        var (access, refresh) = store.Read(ctx);
        if (access is null) return Results.Unauthorized();

        var (status, body) = await api.GetCurrentRawAsync(access, ctx.RequestAborted);
        if (status == 401 && !string.IsNullOrEmpty(refresh))
        {
            var refreshed = await tokens.RefreshAsync(refresh, ctx.RequestAborted);
            if (refreshed is null) return Results.Unauthorized();
            store.Save(ctx, refreshed.access_token, refreshed.refresh_token);
            (status, body) = await api.GetCurrentRawAsync(refreshed.access_token, ctx.RequestAborted);
        }
        return Results.Content(body, "application/json", statusCode: status);
    }
}
