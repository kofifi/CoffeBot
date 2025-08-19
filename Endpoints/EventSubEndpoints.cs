using System.Text;
using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using CoffeBot.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CoffeBot.Endpoints;

public static class EventSubEndpoints
{
    public static IEndpointRouteBuilder MapEventSubEndpoints(this IEndpointRouteBuilder app)
    {
        // UI z subskrypcją, listą subskrypcji i testem webhooka
        app.MapGet("/events", (IEventSubClient? _) =>
            Results.Content($$"""
                <html><body style="font-family:sans-serif">
                  <h2>EventSub</h2>
                  <form method="post" action="/events/subscribe"><button>Subscribe</button></form>
                  <h3>Subskrypcje</h3>
                  <ul id="subs"></ul>
                  <h3>Messages</h3>
                  <ul id="msgs"></ul>
                  <button onclick="sendTest()">Wyślij testową wiadomość</button>
                  <script>
                    // Stream czatu
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
                    es.onerror = e => {
                      const li = document.createElement('li');
                      li.textContent = '[Rozłączono z serwerem]';
                      ul.appendChild(li);
                    };

                    // Lista subskrypcji i usuwanie
                    async function loadSubs() {
                      const res = await fetch('/events/webhooks');
                      if (!res.ok) return;
                      const data = await res.json();
                      const ul = document.getElementById('subs');
                      ul.innerHTML = '';
                      (data.data || []).forEach(sub => {
                        const li = document.createElement('li');
                        li.textContent = sub.id + ' (' + (sub.events ? sub.events.map(e => e.name).join(', ') : '') + ') ';
                        const btn = document.createElement('button');
                        btn.textContent = 'Usuń';
                        btn.onclick = async () => {
                          await fetch('/events/webhooks?id=' + encodeURIComponent(sub.id), { method: 'DELETE' });
                          loadSubs();
                        };
                        li.appendChild(btn);
                        ul.appendChild(li);
                      });
                    }
                    loadSubs();

                    // Testowy webhook
                    function sendTest() {
                      fetch('/events/webhook/test', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                          content: 'Testowa wiadomość z UI',
                          sender: { username: 'TestUser' },
                          created_at: new Date().toISOString()
                        })
                      });
                    }
                  </script>
                </body></html>
            """, "text/html", Encoding.UTF8));

        app.MapGet("/events/stream", async (HttpContext ctx, IChatEventStream stream) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var (id, reader) = stream.Subscribe();
            try
            {
                await foreach (var msg in reader.ReadAllAsync(ctx.RequestAborted))
                {
                    var json = JsonSerializer.Serialize(msg);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", Encoding.UTF8, ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException)
            {
                // Połączenie przerwane przez klienta
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

        app.MapGet("/events/webhooks", async (
            HttpContext ctx,
            IEventSubClient client,
            ITokenStore store,
            CancellationToken ct) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            try
            {
                var json = await client.ListSubscriptionsAsync(access, ct);
                return Results.Text(json, "application/json", Encoding.UTF8);
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem(ex.Message, statusCode: 502);
            }
        });

        app.MapDelete("/events/webhooks", async (
            HttpContext ctx,
            IEventSubClient client,
            ITokenStore store,
            string[] id,
            CancellationToken ct) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            if (id is null || id.Length == 0)
                return Results.BadRequest(new { error = "Brak przekazanych ID subskrypcji do usunięcia." });

            try
            {
                await client.DeleteSubscriptionsAsync(access, ct, id);
                return Results.Ok(new { unsubscribed = id });
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem(ex.Message, statusCode: 502);
            }
        });

        app.MapGet("/events/webhook", (string challenge) => Results.Text(challenge, "text/plain", Encoding.UTF8));

        // Produkcyjny webhook z weryfikacją podpisu
        app.MapPost("/events/webhook", async (
            HttpRequest req,
            KickWebhookVerifier verifier,
            IOptions<KickEventOptions> opt,
            IChatApiClient chatApi,
            IChatEventStream stream,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Webhook");
            logger.LogInformation("Webhook POST received");
            Console.WriteLine("Webhook POST received");

            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            logger.LogInformation("Webhook body: {Body}", body);
            Console.WriteLine($"Webhook body: {body}");

            if (!await verifier.VerifyAsync(req, body, ct))
            {
                logger.LogWarning("Webhook signature verification failed");
                Console.WriteLine("Webhook signature verification failed");
                return Results.Unauthorized();
            }

            return await HandleWebhookBody(body, opt, chatApi, stream, logger, ct);
        });

        // Testowy webhook bez weryfikacji podpisu
        app.MapPost("/events/webhook/test", async (
            HttpRequest req,
            IOptions<KickEventOptions> opt,
            IChatApiClient chatApi,
            IChatEventStream stream,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WebhookTest");
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            logger.LogInformation("Webhook TEST body: {Body}", body);
            return await HandleWebhookBody(body, opt, chatApi, stream, logger, ct);
        });

        return app;
    }

    // Wspólna logika obsługi webhooka
    private static async Task<IResult> HandleWebhookBody(
        string body,
        IOptions<KickEventOptions> opt,
        IChatApiClient chatApi,
        IChatEventStream stream,
        ILogger logger,
        CancellationToken ct)
    {
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

        logger.LogInformation("Parsed event: {User} - {Content}", username, content);
        Console.WriteLine($"Parsed event: {username} - {content}");

        if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(content))
            stream.Publish(new ChatEventMessage(username, content, created));

        if (content.StartsWith("!coffebot", StringComparison.OrdinalIgnoreCase))
        {
            var cfg = opt.Value;
            try
            {
                await chatApi.SendAsync(
                    cfg.BotAccessToken,
                    new ChatSendCommand(
                        "Im coffe bot what can i help you?",
                        "bot",
                        cfg.ChannelId),
                    ct);
                logger.LogInformation("Bot message sent via Kick API");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to send bot response");
            }
        }

        return Results.Ok(new { ok = true, type = "chat.message.sent" });
    }
}