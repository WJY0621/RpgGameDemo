namespace WorkDemoServer.Contracts;

public sealed record ChatMessageResponse(
    string MessageId,
    string SenderPlayerId,
    string ReceiverPlayerId,
    string MessageText,
    DateTimeOffset SentAt);

public sealed record FriendUnreadResponse(
    string FriendPlayerId,
    int UnreadCount);

public sealed record ChatUnreadSummaryResponse(
    int TotalUnreadCount,
    IReadOnlyList<FriendUnreadResponse> Friends);
