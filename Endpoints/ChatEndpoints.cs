using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;

namespace CoffeBot.Endpoints;

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/chat");

        // ---------- SIMPLE WEB FORM UI ----------
        group.MapGet("", (HttpContext ctx, ITokenStore store) =>
        {
            var (access, _) = store.Read(ctx);
            var isAuth = access is not null;

            return Results.Content($$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>Kick Chat – Send</title>
  <style>
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif; margin: 24px; }
    .row { margin: 8px 0; }
    textarea, input[type=text] { width: 100%; padding: 8px; }
    button { padding: 8px 14px; cursor: pointer; }
    .badge { display:inline-block; padding:2px 8px; border-radius:12px; font-size:12px; }
    .ok { background:#e7f7ec; color:#186a3b; }
    .warn { background:#fff3cd; color:#7a5d00; }
    .err { background:#fdecea; color:#7a1c1c; }
    pre { background:#f5f5f5; padding:12px; overflow:auto; }
    fieldset { border:1px solid #ddd; padding:10px; }
    legend { padding:0 6px; color:#666; }
  </style>
</head>
<body>
  <h1>Kick Chat – send message</h1>

  <p>Status:
    {{(isAuth ? "<span class='badge ok'>zalogowany</span>" : "<span class='badge err'>wylogowany</span>")}}
    {{(!isAuth ? " — <a href='/login'>Login</a>" : "")}}
  </p>

  <fieldset>
    <legend>Message</legend>
    <div class="row">
      <label>Content</label>
      <textarea id="content" rows="3" placeholder="Say hello"></textarea>
    </div>
    <div class="row">
      <label>Reply to (optional message_id)</label>
      <input id="replyId" type="text" placeholder="e.g. 6f2c8f7f-...">
    </div>
    <div class="row">
      <label>Send as</label>
      <label><input type="radio" name="sendType" value="user" checked> user</label>
      <label style="margin-left:12px"><input type="radio" name="sendType" value="bot"> bot</label>
    </div>
    <div class="row">
      <button id="sendBtn">Send</button>
      <span id="hint" style="margin-left:8px;color:#666;font-size:13px">
        Requires scope <code>chat:write</code> (re-login if you just added it).
      </span>
    </div>
  </fieldset>

  <h3>Result</h3>
  <div id="resultBadge"></div>
  <pre id="resultBox"></pre>

<script>
  const $ = (id) => document.getElementById(id);

  async function sendChat() {
    const btn = $("sendBtn");
    const content = $("content").value.trim();
    const replyToMessageId = $("replyId").value.trim() || null;
    const type = document.querySelector("input[name='sendType']:checked").value;

    $("resultBadge").innerHTML = "";
    $("resultBox").textContent = "";

    if (!content) {
      $("resultBadge").innerHTML = "<span class='badge warn'>Missing content</span>";
      return;
    }

    const url = type === "bot" ? "/chat/bot" : "/chat/user";
    btn.disabled = true;
    try {
      const resp = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content, replyToMessageId })
      });
      const text = await resp.text();
      let pretty = text;
      try { pretty = JSON.stringify(JSON.parse(text), null, 2); } catch {}

      if (resp.ok) {
        $("resultBadge").innerHTML = "<span class='badge ok'>OK " + resp.status + "</span>";
      } else {
        $("resultBadge").innerHTML = "<span class='badge err'>Error " + resp.status + "</span>";
      }
      $("resultBox").textContent = pretty;
    } catch (e) {
      $("resultBadge").innerHTML = "<span class='badge err'>Network error</span>";
      $("resultBox").textContent = String(e);
    } finally {
      btn.disabled = false;
    }
  }

  $("sendBtn").addEventListener("click", sendChat);
</script>
</body>
</html>
""", "text/html");
        });

        // ---------- API: send as USER ----------
        group.MapPost("user", async (HttpContext ctx, ITokenStore store, IUserApiClient users, IChatApiClient chat) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            var dto = await JsonSerializer.DeserializeAsync<SendBody>(ctx.Request.Body, JsonOpts)
                      ?? new SendBody(string.Empty, null);
            if (string.IsNullOrWhiteSpace(dto.Content))
                return Results.BadRequest("Missing content");

            var (status, userJson) = await users.GetCurrentRawAsync(access, ctx.RequestAborted);
            if (status != 200) return Results.StatusCode(status);

            using var doc = JsonDocument.Parse(userJson);
            var userId = doc.RootElement.GetProperty("data")[0].GetProperty("user_id").GetInt32();

            var result = await chat.SendAsync(
                access,
                new ChatSendCommand(dto.Content, "user", userId, dto.ReplyToMessageId),
                ctx.RequestAborted);

            return Results.Json(result);
        });

        // ---------- API: send as BOT ----------
        group.MapPost("bot", async (HttpContext ctx, ITokenStore store, IChatApiClient chat) =>
        {
            var (access, _) = store.Read(ctx);
            if (access is null) return Results.Unauthorized();

            var dto = await JsonSerializer.DeserializeAsync<SendBody>(ctx.Request.Body, JsonOpts)
                      ?? new SendBody(string.Empty, null);
            if (string.IsNullOrWhiteSpace(dto.Content))
                return Results.BadRequest("Missing content");

            var result = await chat.SendAsync(
                access,
                new ChatSendCommand(dto.Content, "bot", null, dto.ReplyToMessageId),
                ctx.RequestAborted);

            return Results.Json(result);
        });

        return app;
    }

    private record SendBody(string Content, string? ReplyToMessageId);
}
