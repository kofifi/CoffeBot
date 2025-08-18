using CoffeBot.Abstractions;
using CoffeBot.Features.Auth;
using CoffeBot.Features.Users;
using CoffeBot.Http;
using CoffeBot.Options;
using CoffeBot.Services;
using DotNetEnv;
// DO NOT import CoffeBot.Endpoints broadly
using ChatEndpoints = CoffeBot.Endpoints.ChatEndpoints;
using KickApiEndpoints = CoffeBot.Endpoints.KickProxyEndpoints;
using ChatListenEndpoints = CoffeBot.Endpoints.ChatListenEndpoints;
using EventSubEndpoints = CoffeBot.Endpoints.EventSubEndpoints;

Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Options & Http clients
builder.Services.Configure<KickOptions>(builder.Configuration.GetSection("Kick"));
builder.Services.Configure<KickChatOptions>(builder.Configuration.GetSection("KickChat"));
builder.Services.Configure<KickEventOptions>(builder.Configuration.GetSection("KickEvent"));
builder.Services.AddKickHttpClients();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(8);
});

// Prefer the feature DI to avoid duplicates
builder.Services.AddCoreServices();
builder.Services.AddAuthFeature();
builder.Services.AddUsersFeature();

// If Chat DI isn’t included in the features yet:
builder.Services.AddSingleton<IChatApiClient, ChatApiClient>();
builder.Services.AddSingleton<IChatListener, ChatListener>();
builder.Services.AddScoped<IEventSubClient, EventSubClient>();
builder.Services.AddSingleton<KickWebhookVerifier>();

var app = builder.Build();
app.UseSession();

// Home
app.MapGet("/", (HttpContext ctx) =>
{
    var (access, _) = ctx.RequestServices.GetRequiredService<ITokenStore>().Read(ctx);
    var isAuth = access is not null;

    return Results.Content($$"""
                             <html><body style="font-family:sans-serif">
                               <h1>Kick OAuth2 (PKCE)</h1>
                               <p>Status: <b>{{(isAuth ? "zalogowany" : "wylogowany")}}</b></p>
                               <p><a href="/login">Login</a> |
                                  <a href="/users/me">/users/me</a> |
                                  <a href="/chat">/chat</a> |
                                  <a href="/chat/listen">/chat/listen</a> |
                                  <a href="/events">/events</a> |
                                  <a href="/logout">Logout</a></p>
                             </body></html>
                             """, "text/html");
});

// Use ONLY the Features endpoint mappers for auth & users:
AuthEndpoints.MapAuthEndpoints(app);
MeEndpoints.MapUserEndpoints(app);

// Bring Chat from legacy with the alias:
ChatEndpoints.MapChatEndpoints(app);
KickApiEndpoints.MapKickProxyEndpoints(app);
ChatListenEndpoints.MapChatListenEndpoints(app);
EventSubEndpoints.MapEventSubEndpoints(app);

var listenUrl = Environment.GetEnvironmentVariable("APP__ListenUrl");
if (!string.IsNullOrWhiteSpace(listenUrl)) app.Run(listenUrl);
else app.Run();