using CoffeBot.Abstractions;
using CoffeBot.Features.Auth;
using CoffeBot.Features.Users;
using CoffeBot.Http;
using CoffeBot.Options;
using CoffeBot.Services;
using Microsoft.AspNetCore.Http.Extensions;
using DotNetEnv;
using CoffeBot.Endpoints;


Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<KickOptions>(builder.Configuration.GetSection("Kick"));
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
builder.Services.AddSingleton<IPkceService, PkceService>();
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IAuthUrlBuilder, AuthUrlBuilder>();
builder.Services.AddSingleton<ITokenStore, SessionTokenStore>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserApiClient, UserApiClient>();
builder.Services.AddScoped<IChatApiClient, ChatApiClient>();

var app = builder.Build();
app.UseSession();

// Strona startowa (opcjonalnie tu zostaje)
// Program.cs (your current home)
app.MapGet("/", (HttpContext ctx) =>
{
    var (access, _) = ctx.RequestServices.GetRequiredService<ITokenStore>().Read(ctx);
    var meUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/users/me");
    var loginUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/login");
    var logoutUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/logout");
    var chatUrl = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, "/chat");


    var isAuth = access is not null;
    return Results.Content($$"""
    <html><body style="font-family:sans-serif">
      <h1>Kick OAuth2 (PKCE)</h1>
      <p>Status: <b>{{(isAuth ? "zalogowany" : "wylogowany")}}</b></p>
      <p><a href="{{loginUrl}}">Login</a> |
         <a href="{{meUrl}}">/users/me</a> |
         <a href="{{logoutUrl}}">Logout</a></p>
         <a href="{{chatUrl}}">/chat</a></p>
    </body></html>
    """, "text/html");
});

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapChatEndpoints();


var listenUrl = Environment.GetEnvironmentVariable("APP__ListenUrl");
if (!string.IsNullOrWhiteSpace(listenUrl))
    app.Run(listenUrl);
else
    app.Run();