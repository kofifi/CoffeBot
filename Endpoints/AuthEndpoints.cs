using CoffeBot.Abstractions;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/");

        // /login
        group.MapGet("login", (HttpContext ctx, IPkceService pkce, IStateService state, IAuthUrlBuilder urlBuilder, IOptions<KickOptions> opt) =>
        {
            var (verifier, challenge) = pkce.CreatePair();
            var st = pkce.CreateRandomBase64Url(16);
            var nonce = pkce.CreateRandomBase64Url(16);

            state.Save(ctx, st, verifier, nonce);

            var url = urlBuilder.BuildAuthorizeUrl(st, challenge, opt.Value.Scope);
            return Results.Redirect(url);
        });

        // /callback
        group.MapGet("callback", async (HttpContext ctx, IStateService stateSvc, ITokenService tokens, ITokenStore store) =>
        {
            var code  = ctx.Request.Query["code"].ToString();
            var state = ctx.Request.Query["state"].ToString();

            var (expectedState, codeVerifier, _) = stateSvc.Read(ctx);
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != expectedState || string.IsNullOrEmpty(codeVerifier))
                return Results.BadRequest("Invalid state or code.");

            var tok = await tokens.ExchangeCodeAsync(code, codeVerifier!, ctx.RequestAborted);
            store.Save(ctx, tok.access_token, tok.refresh_token);
            stateSvc.Clear(ctx);

            return Results.Redirect("/");
        });

        // /logout
        group.MapGet("logout", async (HttpContext ctx, ITokenStore store, ITokenService tokens) =>
        {
            var (access, refresh) = store.Read(ctx);
            if (!string.IsNullOrEmpty(access))  await tokens.RevokeAsync(access,  "access_token",  ctx.RequestAborted);
            if (!string.IsNullOrEmpty(refresh)) await tokens.RevokeAsync(refresh, "refresh_token", ctx.RequestAborted);
            store.Clear(ctx);
            return Results.Redirect("/");
        });

        return app;
    }
}
