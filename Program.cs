using CoffeBot.Abstractions;
using CoffeBot.Auth;
using CoffeBot.Http;
using CoffeBot.Options;
using CoffeBot.Services;
using Microsoft.AspNetCore.Http.Extensions;
using DotNetEnv; // <-- new

// 1) Load .env BEFORE building the host, so env vars are visible to Configuration
Env.Load();

// 2) Build the host (appsettings.json -> appsettings.{env}.json -> Environment Variables)
//    Because we loaded .env already, its vars are now normal environment variables.
var builder = WebApplication.CreateBuilder(args);

// 3) (Optional) also add environment variables provider explicitly; harmless if already present
builder.Configuration.AddEnvironmentVariables();

// 4) Bind KickOptions from Configuration (env overrides appsettings automatically)
builder.Services.Configure<KickOptions>(builder.Configuration.GetSection("Kick"));

// 5) Http clients, session, DI as before
builder.Services.AddKickHttpClients();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(8);
});

// DI: interfejsy → implementacje
builder.Services.AddSingleton<IPkceService, PkceService>();
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IAuthUrlBuilder, AuthUrlBuilder>();
builder.Services.AddSingleton<ITokenStore, SessionTokenStore>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserApiClient, UserApiClient>();

var app = builder.Build();
app.UseSession();

// Home
app.MapGet("/", (HttpContext ctx) =>
{
    var (access, _) = ctx.RequestServices.GetRequiredService<ITokenStore>().Read(ctx);
    var meUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/me");
    var loginUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/login");
    var logoutUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/logout");

    var isAuth = access is not null;
    return Results.Content($$"""
    <html><body style="font-family:sans-serif">
      <h1>Kick OAuth2 (PKCE)</h1>
      <p>Status: <b>{{(isAuth ? "zalogowany" : "wylogowany")}}</b></p>
      <p><a href="{{loginUrl}}">Login</a> |
         <a href="{{meUrl}}">/me</a> |
         <a href="{{logoutUrl}}">Logout</a></p>
    </body></html>
    """, "text/html");
});

// 1) /login
app.MapGet("/login", (HttpContext ctx, IPkceService pkce, IStateService state, IAuthUrlBuilder urlBuilder, Microsoft.Extensions.Options.IOptions<KickOptions> opt) =>
{
    var (verifier, challenge) = pkce.CreatePair();
    var st = pkce.CreateRandomBase64Url(16);
    var nonce = pkce.CreateRandomBase64Url(16);

    state.Save(ctx, st, verifier, nonce);

    var url = urlBuilder.BuildAuthorizeUrl(st, challenge, opt.Value.Scope);
    return Results.Redirect(url);
});

// 2) /callback
app.MapGet("/callback", async (HttpContext ctx, IStateService stateSvc, ITokenService tokens, ITokenStore store) =>
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

// 3) /me (raw passthrough + refresh)
app.MapGet("/me", async (HttpContext ctx, ITokenStore store, ITokenService tokens, IUserApiClient api) =>
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

// 4) /logout
app.MapGet("/logout", async (HttpContext ctx, ITokenStore store, ITokenService tokens) =>
{
    var (access, refresh) = store.Read(ctx);
    if (!string.IsNullOrEmpty(access))  await tokens.RevokeAsync(access,  "access_token",  ctx.RequestAborted);
    if (!string.IsNullOrEmpty(refresh)) await tokens.RevokeAsync(refresh, "refresh_token", ctx.RequestAborted);
    store.Clear(ctx);
    return Results.Redirect("/");
});

// 6) Respect APP__ListenUrl if provided; otherwise just Run() and let Kestrel config apply
var listenUrl = Environment.GetEnvironmentVariable("APP__ListenUrl");
if (!string.IsNullOrWhiteSpace(listenUrl))
    app.Run(listenUrl);
else
    app.Run();
