using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoffeBot.Abstractions;
using CoffeBot.Models;
using CoffeBot.Options;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class ChatListener : IChatListener, IDisposable
{
    private readonly IChatApiClient _chatApi;
    private readonly KickChatOptions _opt;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private volatile bool _running;

    private string? _accessToken;
    private int _channelId;

    public bool IsRunning => _running;

    public ChatListener(IChatApiClient chatApi, IOptions<KickChatOptions> opt)
    {
        _chatApi = chatApi;
        _opt = opt.Value;
    }

    public Task StartAsync(string accessToken, int channelId, CancellationToken ct)
    {
        if (_running) return Task.CompletedTask;

        _accessToken = accessToken;
        _channelId = channelId;

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _running = true;
        _loopTask = RunLoopAsync(_loopCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _running = false;
        try { _loopCts?.Cancel(); } catch { }
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch { }
        }
        if (_ws is { State: WebSocketState.Open })
        {
            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", ct); } catch { }
        }
        _ws?.Dispose();
        _ws = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);

        while (_running && !ct.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                _ws.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
                var uri = new Uri($"{_opt.WebSocketUrl}?channel_id={_channelId}");
                await _ws.ConnectAsync(uri, ct);

                var buffer = new byte[64 * 1024];
                var sb = new StringBuilder();

                while (_running && _ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    var result = await _ws.ReceiveAsync(segment, ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage) continue;

                    var json = sb.ToString();
                    sb.Clear();
                    HandleIncoming(json);
                }

                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                await Task.Delay(backoff, ct);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
            }
            finally
            {
                try
                {
                    if (_ws is { State: WebSocketState.Open })
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", ct);
                }
                catch { }
                _ws?.Dispose();
                _ws = null;
            }
        }
    }

    private void HandleIncoming(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            if (!string.Equals(typeEl.GetString(), "message", StringComparison.OrdinalIgnoreCase)) return;
            if (!root.TryGetProperty("data", out var data)) return;
            if (!data.TryGetProperty("content", out var contentEl)) return;

            var content = contentEl.GetString() ?? string.Empty;
            if (content.StartsWith("!coffebot", StringComparison.OrdinalIgnoreCase))
            {
                _ = RespondAsync();
            }
        }
        catch
        {
            // ignore parse errors
        }
    }

    private async Task RespondAsync()
    {
        if (_accessToken is null) return;
        try
        {
            await _chatApi.SendAsync(
                _accessToken,
                new ChatSendCommand("☕ CoffeeBot here! Type !coffebot help for commands.", "user", _channelId, null),
                CancellationToken.None);
        }
        catch
        {
            // ignore errors
        }
    }

    public void Dispose()
    {
        _ = StopAsync(CancellationToken.None);
        _loopCts?.Dispose();
    }
}

