using CoffeBot.Abstractions;
using CoffeBot.Features.Auth;
using CoffeBot.Features.Users;
using CoffeBot.Http;
using CoffeBot.Options;
using CoffeBot.Services;
using Microsoft.AspNetCore.Http.Extensions;
using DotNetEnv;

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

builder.Services.AddCoreServices();
builder.Services.AddAuthFeature();
builder.Services.AddUsersFeature();

var app = builder.Build();
app.UseSession();

// Home
app.MapGet("/", (HttpContext ctx) =>
{
    var (access, _) = ctx.RequestServices.GetRequiredService<ITokenStore>().Read(ctx);
    var meUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/users/me");
    var loginUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/login");
    var logoutUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/logout");

    var isAuth = access is not null;
    return Results.Content($$"""
    <html><body style="font-family:sans-serif">
      <h1>Kick OAuth2 (PKCE)</h1>
      <p>Status: <b>{{(isAuth ? "zalogowany" : "wylogowany")}}</b></p>
      <p><a href="{{loginUrl}}">Login</a> |
         <a href="{{meUrl}}">/users/me</a> |
         <a href="{{logoutUrl}}">Logout</a></p>
    </body></html>
    """, "text/html");
});

app.MapAuthEndpoints();
app.MapUserEndpoints();

// 6) Respect APP__ListenUrl if provided; otherwise just Run() and let Kestrel config apply
var listenUrl = Environment.GetEnvironmentVariable("APP__ListenUrl");
if (!string.IsNullOrWhiteSpace(listenUrl))
    app.Run(listenUrl);
else
    app.Run();
