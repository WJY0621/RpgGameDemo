namespace WorkDemoServer.Contracts;

public sealed record SendOnlineInviteRequest(
    string TargetPlayerId,
    string InviteType,
    string Address,
    int Port,
    string? RelayJoinCode,
    string? LobbyId);

public sealed record ReplyOnlineInviteRequest(
    bool Accepted,
    string Address,
    int Port,
    string? RelayJoinCode,
    string? LobbyId);
