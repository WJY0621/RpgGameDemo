namespace WorkDemoServer.Models;

public sealed class FriendshipRecord
{
    public string PlayerAId { get; set; } = string.Empty;

    public string PlayerBId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
