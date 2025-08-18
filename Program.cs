using CoffeBot.Abstractions;
using CoffeBot.Auth;
using CoffeBot.Http;
using CoffeBot.Options;
using CoffeBot.Services;
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

// DI
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
    var isAuth = access is not null;
    return Results.Content($$"""
                             <html><body style="font-family:sans-serif">
                               <h1>Kick OAuth2 (PKCE)</h1>
                               <p>Status: <b>{{(isAuth ? "zalogowany" : "wylogowany")}}</b></p>
                               <p>
                                 <a href="/login">Login</a> |
                                 <a href="/me">/me</a> |
                                 <a href="/chat">/chat</a> |
                                 <a href="/logout">Logout</a>
                               </p>
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