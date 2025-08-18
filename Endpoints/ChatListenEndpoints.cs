using System.Text.Json;
using CoffeBot.Abstractions;

namespace CoffeBot.Endpoints;

public static class ChatListenEndpoints
{
    public static IEndpointRouteBuilder MapChatListenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chat/listen/start", async (HttpContext ctx, IChatListener listener, IUserApiClient users, ITokenStore store, CancellationToken ct) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            var (status, body) = await users.GetCurrentRawAsync(access, ct);
            if (status != 200) return Results.StatusCode(status);

            using var doc = JsonDocument.Parse(body);
            var channelId = doc.RootElement.GetProperty("data")[0].GetProperty("user_id").GetInt32();

            await listener.StartAsync(access, channelId, ct);
            return Results.Ok(new { started = true, channelId });
        });

        app.MapPost("/chat/listen/stop", async (IChatListener listener, CancellationToken ct) =>
        {
            await listener.StopAsync(ct);
            return Results.Ok(new { stopped = true });
        });

        app.MapGet("/chat/listen", (IChatListener listener) =>
            Results.Content($$"""
                <html><body style="font-family:sans-serif">
                  <h2>Chat listener</h2>
                  <p>Status: <b>{{(listener.IsRunning ? "running" : "stopped")}}</b></p>
                  <form method="post" action="/chat/listen/start"><button>Start</button></form>
                  <form method="post" action="/chat/listen/stop" style="margin-top:8px"><button>Stop</button></form>
                </body></html>
                """, "text/html"));

        return app;
    }
}

