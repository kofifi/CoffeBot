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
                  <h3>Messages</h3>
                  <ul id="msgs"></ul>
                  <script>
                    const ul = document.getElementById('msgs');
                    const es = new EventSource('/events/stream');
                    es.onmessage = e => {
                      try {
                        const msg = JSON.parse(e.data);
                        const li = document.createElement('li');
                        li.textContent = msg.username + ': ' + msg.content;
                        ul.appendChild(li);
                      } catch {}
                    };
                  </script>
                </body></html>
            """, "text/html"));

        app.MapGet("/events/stream", async (HttpContext ctx, IChatEventStream stream) =>
        {
            ctx.Response.Headers.Add("Content-Type", "text/event-stream");
            var (id, reader) = stream.Subscribe();
            try
            {
                await foreach (var msg in reader.ReadAllAsync(ctx.RequestAborted))
                {
                    var json = JsonSerializer.Serialize(msg);
                    await ctx.Response.WriteAsync($"data: {json}\n\n");
                    await ctx.Response.Body.FlushAsync();
                }
            }
            finally
            {
                stream.Unsubscribe(id);
            }
        });

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
            IChatEventStream stream,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            if (!await verifier.VerifyAsync(req, body, ct)) return Results.Unauthorized();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string content = string.Empty;
            string username = string.Empty;
            DateTime created = DateTime.UtcNow;

            if (root.TryGetProperty("content", out var contentEl))
                content = contentEl.GetString() ?? string.Empty;
            if (root.TryGetProperty("sender", out var senderEl) &&
                senderEl.TryGetProperty("username", out var userEl))
                username = userEl.GetString() ?? string.Empty;
            if (root.TryGetProperty("created_at", out var createdEl) &&
                createdEl.TryGetDateTime(out var dt))
                created = dt;

            if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(content))
                stream.Publish(new ChatEventMessage(username, content, created));

            if (content.StartsWith("!coffebot", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = opt.Value;
                var reply = new ChatSendCommand("Im coffe bot what can i help you?", "user", cfg.ChannelId);
                if (!string.IsNullOrWhiteSpace(cfg.BotAccessToken))
                    await chatApi.SendAsync(cfg.BotAccessToken, reply, ct);
            }

            return Results.Ok(new { ok = true, type = "chat.message.sent" });
        });

        return app;
    }
}
