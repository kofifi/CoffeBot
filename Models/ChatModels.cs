namespace CoffeBot.Models;

public record ChatSendCommand(
    string Content,
    string Type,                 // "user" | "bot"
    int? BroadcasterUserId = null,
    string? ReplyToMessageId = null);

// API success payload (per docs)
public record ChatSendResult(bool is_sent, string message_id);