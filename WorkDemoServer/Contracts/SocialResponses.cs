namespace WorkDemoServer.Contracts;

public sealed record FriendResponse(
    AccountProfileResponse Profile,
    bool IsOnline);

public sealed record FriendRequestResponse(
    string RequestId,
    AccountProfileResponse Requester,
    DateTimeOffset CreatedAt);

public sealed record SocialActionResponse(string Message);
