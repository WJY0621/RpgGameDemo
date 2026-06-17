using WorkDemoServer.Contracts;
using System.Collections.Concurrent;

namespace WorkDemoServer.Services;

public sealed class OnlineInviteService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromSeconds(120);

    private readonly ConcurrentDictionary<string, OnlineInviteResponse> pendingInvites = new();

    private readonly AccountService accounts;
    private readonly ISocialRepository socialRepository;
    private readonly RealtimeConnectionHub realtimeHub;

    public OnlineInviteService(
        AccountService accounts,
        ISocialRepository socialRepository,
        RealtimeConnectionHub realtimeHub)
    {
        this.accounts = accounts;
        this.socialRepository = socialRepository;
        this.realtimeHub = realtimeHub;
    }

    public async Task<OnlineInviteResult> SendInviteAsync(
        string requesterPlayerId,
        SendOnlineInviteRequest request,
        CancellationToken cancellationToken)
    {
        PruneExpiredInvites();

        var targetPlayerId = request.TargetPlayerId.Trim();
        if (string.IsNullOrWhiteSpace(targetPlayerId))
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Target player ID is required.");
        }

        if (string.Equals(requesterPlayerId, targetPlayerId, StringComparison.Ordinal))
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "You cannot invite yourself.");
        }

        if (!TryNormalizeInviteType(request.InviteType, out var inviteType))
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Invite type is invalid.");
        }

        if (request.Port <= 0 || request.Port > ushort.MaxValue)
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Port is invalid.");
        }

        var requester = await accounts.GetProfileAsync(requesterPlayerId, cancellationToken);
        var target = await accounts.GetProfileAsync(targetPlayerId, cancellationToken);
        if (requester == null || target == null)
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.AccountNotFound, "Account does not exist.");
        }

        if (!await socialRepository.AreFriendsAsync(requesterPlayerId, targetPlayerId, cancellationToken))
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.NotFriends, "Players are not friends.");
        }

        if (!realtimeHub.IsConnected(targetPlayerId))
        {
            return OnlineInviteResult.Fail(OnlineInviteErrorCode.TargetOffline, "Target player is offline.");
        }

        var invite = new OnlineInviteResponse(
            Guid.NewGuid().ToString("N"),
            inviteType,
            requesterPlayerId,
            string.IsNullOrWhiteSpace(requester.DisplayName) ? requester.PlayerId : requester.DisplayName,
            inviteType == "InviteToMyWorld" ? requesterPlayerId : targetPlayerId,
            targetPlayerId,
            string.IsNullOrWhiteSpace(request.Address) ? "127.0.0.1" : request.Address.Trim(),
            request.Port,
            request.RelayJoinCode?.Trim() ?? string.Empty,
            request.LobbyId?.Trim() ?? string.Empty,
            DateTimeOffset.UtcNow);

        pendingInvites[invite.InviteId] = invite;
        return OnlineInviteResult.Success(invite);
    }

    public async Task<(OnlineInviteResult Result, OnlineInviteResultResponse? Response)> ReplyInviteAsync(
        string targetPlayerId,
        string inviteId,
        ReplyOnlineInviteRequest request,
        CancellationToken cancellationToken)
    {
        PruneExpiredInvites();

        if (string.IsNullOrWhiteSpace(inviteId))
        {
            return (OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Invite ID is required."), null);
        }

        if (!pendingInvites.TryRemove(inviteId, out var invite) ||
            !string.Equals(invite.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
        {
            return (OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Invite does not exist."), null);
        }

        if (IsExpired(invite))
        {
            return (OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Invite has expired. Please send a new invite."), null);
        }

        if (request.Accepted &&
            (request.Port <= 0 || request.Port > ushort.MaxValue))
        {
            return (OnlineInviteResult.Fail(OnlineInviteErrorCode.Validation, "Port is invalid."), null);
        }

        var requester = await accounts.GetProfileAsync(invite.RequesterPlayerId, cancellationToken);
        if (requester == null)
        {
            return (OnlineInviteResult.Fail(OnlineInviteErrorCode.AccountNotFound, "Requester account does not exist."), null);
        }

        var address = string.IsNullOrWhiteSpace(request.Address)
            ? invite.Address
            : request.Address.Trim();
        var port = request.Port > 0 ? request.Port : invite.Port;
        var relayJoinCode = string.IsNullOrWhiteSpace(request.RelayJoinCode)
            ? invite.RelayJoinCode
            : request.RelayJoinCode.Trim();
        var lobbyId = string.IsNullOrWhiteSpace(request.LobbyId)
            ? invite.LobbyId
            : request.LobbyId.Trim();

        var result = new OnlineInviteResultResponse(
            invite.InviteId,
            invite.InviteType,
            invite.RequesterPlayerId,
            invite.RequesterDisplayName,
            invite.InviteType == "RequestToJoinWorld" ? targetPlayerId : invite.HostPlayerId,
            invite.TargetPlayerId,
            address,
            port,
            relayJoinCode,
            lobbyId,
            request.Accepted,
            DateTimeOffset.UtcNow);

        return (OnlineInviteResult.Success(invite), result);
    }

    private void PruneExpiredInvites()
    {
        foreach (var pair in pendingInvites)
        {
            if (IsExpired(pair.Value))
            {
                pendingInvites.TryRemove(pair.Key, out _);
            }
        }
    }

    private static bool IsExpired(OnlineInviteResponse invite)
    {
        return DateTimeOffset.UtcNow - invite.CreatedAt > InviteLifetime;
    }

    private static bool TryNormalizeInviteType(string inviteType, out string normalized)
    {
        normalized = string.Empty;
        if (string.Equals(inviteType, "InviteToMyWorld", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "InviteToMyWorld";
            return true;
        }

        if (string.Equals(inviteType, "RequestToJoinWorld", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "RequestToJoinWorld";
            return true;
        }

        return false;
    }
}
