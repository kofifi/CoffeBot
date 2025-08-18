namespace CoffeBot.Options;

public sealed class KickEventOptions
{
    // Secret used to verify webhook signatures
    public string WebhookSecret { get; set; } = string.Empty;

    // Absolute callback URL registered with Kick when subscribing
    public string CallbackUrl { get; set; } = string.Empty;

    // Optional bot token and channel to reply when commands are seen
    public string BotAccessToken { get; set; } = string.Empty;
    public int ChannelId { get; set; }
}
