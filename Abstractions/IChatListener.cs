namespace CoffeBot.Abstractions;

public interface IChatListener
{
    Task StartAsync(string accessToken, int channelId, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    bool IsRunning { get; }
}

