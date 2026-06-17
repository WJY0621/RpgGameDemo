namespace WorkDemoServer.Models;

public sealed class ChatMessageRecord
{
    public string MessageId { get; set; } = string.Empty;

    public string SenderPlayerId { get; set; } = string.Empty;

    public string ReceiverPlayerId { get; set; } = string.Empty;

    public string MessageText { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
