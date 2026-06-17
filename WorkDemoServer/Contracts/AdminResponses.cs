namespace WorkDemoServer.Contracts;

public sealed record AccountAdminResponse(
    string AccountName,
    string PlayerId,
    string DisplayName,
    DateTimeOffset CreatedAt,
    bool IsOnline,
    int FriendCount,
    int IncomingFriendRequestCount);
