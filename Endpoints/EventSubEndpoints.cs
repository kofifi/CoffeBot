using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using CoffeBot.Services;
using Microsoft.Extensions.Options;

namespace CoffeBot.Endpoints;

public static class EventSubEndpoints
{
    public static IEndpointRouteBuilder MapEventSubEndpoints(this IEndpointRouteBuilder app)
    {
        // Simple UI with subscribe button
        app.MapGet("/events", (IEventSubClient? _) =>
            Results.Content($$"""
                <html><body style="font-family:sans-serif">
                  <h2>EventSub</h2>
                  <form method="post" action="/events/subscribe"><button>Subscribe</button></form>
                </body></html>
            """, "text/html"));

        app.MapPost("/events/subscribe", async (
            HttpContext ctx,
            IEventSubClient client,
            IUserApiClient users,
            ITokenStore store,
            CancellationToken ct) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            var me = await users.GetCurrentAsync(access, ct);
            try
            {
                await client.SubscribeToChatAsync(access, me.Id, ct);
                return Results.Ok(new { subscribed = true });
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem(ex.Message, statusCode: 502);
            }
        });

        // Verification challenge
        app.MapGet("/events/webhook", (string challenge) => Results.Text(challenge, "text/plain"));

        // Incoming webhook events
        app.MapPost("/events/webhook", async (
            HttpRequest req,
            KickWebhookVerifier verifier,
            IOptions<KickEventOptions> opt,
            IChatApiClient chatApi,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            if (!await verifier.VerifyAsync(req, body, ct)) return Results.Unauthorized();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("content", out var contentEl))
            {
                var content = contentEl.GetString() ?? string.Empty;
                if (content.StartsWith("!coffebot", StringComparison.OrdinalIgnoreCase))
                {
                    var cfg = opt.Value;
                    var reply = new ChatSendCommand("Im coffe bot what can i help you?", "user", cfg.ChannelId);
                    if (!string.IsNullOrWhiteSpace(cfg.BotAccessToken))
                        await chatApi.SendAsync(cfg.BotAccessToken, reply, ct);
                }
            }
            return Results.Ok();
        });

        return app;
    }
}
