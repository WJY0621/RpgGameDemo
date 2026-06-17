namespace WorkDemoServer.Models;

public sealed class SocialStoreData
{
    public List<FriendRequestRecord> FriendRequests { get; set; } = new();

    public List<FriendshipRecord> Friendships { get; set; } = new();

    public List<ChatMessageRecord> ChatMessages { get; set; } = new();
}
