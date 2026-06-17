namespace WorkDemoServer.Contracts;

public sealed record OnlineInviteResponse(
    string InviteId,
    string InviteType,
    string RequesterPlayerId,
    string RequesterDisplayName,
    string HostPlayerId,
    string TargetPlayerId,
    string Address,
    int Port,
    string RelayJoinCode,
    string LobbyId,
    DateTimeOffset CreatedAt);

public sealed record OnlineInviteResultResponse(
    string InviteId,
    string InviteType,
    string RequesterPlayerId,
    string RequesterDisplayName,
    string HostPlayerId,
    string TargetPlayerId,
    string Address,
    int Port,
    string RelayJoinCode,
    string LobbyId,
    bool Accepted,
    DateTimeOffset RespondedAt);
