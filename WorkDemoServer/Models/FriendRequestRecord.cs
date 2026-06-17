namespace WorkDemoServer.Models;

public sealed class FriendRequestRecord
{
    public string RequestId { get; set; } = string.Empty;

    public string RequesterPlayerId { get; set; } = string.Empty;

    public string TargetPlayerId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
