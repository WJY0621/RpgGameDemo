using WorkDemoServer.Contracts;
using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public sealed class SocialService
{
    private readonly AccountService accounts;
    private readonly ISocialRepository socialRepository;
    private readonly RealtimeConnectionHub realtimeHub;

    public SocialService(
        AccountService accounts,
        ISocialRepository socialRepository,
        RealtimeConnectionHub realtimeHub)
    {
        this.accounts = accounts;
        this.socialRepository = socialRepository;
        this.realtimeHub = realtimeHub;
    }

    public Task<AccountProfileResponse?> SearchPlayerAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        return accounts.GetProfileAsync(playerId, cancellationToken);
    }

    public async Task<SocialResult> SendFriendRequestAsync(
        string requesterPlayerId,
        string targetPlayerId,
        CancellationToken cancellationToken)
    {
        targetPlayerId = targetPlayerId.Trim();
        if (string.IsNullOrWhiteSpace(targetPlayerId))
        {
            return SocialResult.Fail(SocialErrorCode.Validation, "Target player ID is required.");
        }

        if (string.Equals(requesterPlayerId, targetPlayerId, StringComparison.Ordinal))
        {
            return SocialResult.Fail(SocialErrorCode.Validation, "You cannot add yourself as a friend.");
        }

        var targetProfile = await accounts.GetProfileAsync(targetPlayerId, cancellationToken);
        if (targetProfile == null)
        {
            return SocialResult.Fail(SocialErrorCode.AccountNotFound, "Target account does not exist.");
        }

        var alreadyFriends = await socialRepository.AreFriendsAsync(
            requesterPlayerId,
            targetPlayerId,
            cancellationToken);
        if (alreadyFriends)
        {
            return SocialResult.Fail(SocialErrorCode.AlreadyFriends, "Players are already friends.");
        }

        var existingForwardRequest = await socialRepository.FindFriendRequestAsync(
            requesterPlayerId,
            targetPlayerId,
            cancellationToken);
        var existingReverseRequest = await socialRepository.FindFriendRequestAsync(
            targetPlayerId,
            requesterPlayerId,
            cancellationToken);
        if (existingForwardRequest != null || existingReverseRequest != null)
        {
            return SocialResult.Fail(SocialErrorCode.RequestAlreadyExists, "A friend request already exists.");
        }

        await socialRepository.AddFriendRequestAsync(
            new FriendRequestRecord
            {
                RequestId = Guid.NewGuid().ToString("N"),
                RequesterPlayerId = requesterPlayerId,
                TargetPlayerId = targetPlayerId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        return SocialResult.Success();
    }

    public async Task<IReadOnlyList<FriendRequestResponse>> GetIncomingFriendRequestsAsync(
        string targetPlayerId,
        CancellationToken cancellationToken)
    {
        var requests = await socialRepository.GetIncomingFriendRequestsAsync(targetPlayerId, cancellationToken);
        var responses = new List<FriendRequestResponse>();

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var requester = await accounts.GetProfileAsync(request.RequesterPlayerId, cancellationToken);
            if (requester == null)
            {
                continue;
            }

            responses.Add(new FriendRequestResponse(request.RequestId, requester, request.CreatedAt));
        }

        return responses;
    }

    public async Task<SocialResult> AcceptFriendRequestAsync(
        string targetPlayerId,
        string requestId,
        CancellationToken cancellationToken)
    {
        var request = await socialRepository.FindFriendRequestByIdAsync(requestId, cancellationToken);
        if (request == null ||
            !string.Equals(request.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
        {
            return SocialResult.Fail(SocialErrorCode.RequestNotFound, "Friend request does not exist.");
        }

        var requesterProfile = await accounts.GetProfileAsync(request.RequesterPlayerId, cancellationToken);
        if (requesterProfile == null)
        {
            await socialRepository.RemoveFriendRequestAsync(request.RequestId, cancellationToken);
            return SocialResult.Fail(SocialErrorCode.AccountNotFound, "Requester account does not exist.");
        }

        var alreadyFriends = await socialRepository.AreFriendsAsync(
            request.RequesterPlayerId,
            request.TargetPlayerId,
            cancellationToken);
        if (!alreadyFriends)
        {
            await socialRepository.AddFriendshipAsync(
                new FriendshipRecord
                {
                    PlayerAId = request.RequesterPlayerId,
                    PlayerBId = request.TargetPlayerId,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        await socialRepository.RemoveFriendRequestsBetweenAsync(
            request.RequesterPlayerId,
            request.TargetPlayerId,
            cancellationToken);

        return SocialResult.Success();
    }

    public async Task<SocialResult> RefuseFriendRequestAsync(
        string targetPlayerId,
        string requestId,
        CancellationToken cancellationToken)
    {
        var request = await socialRepository.FindFriendRequestByIdAsync(requestId, cancellationToken);
        if (request == null ||
            !string.Equals(request.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
        {
            return SocialResult.Fail(SocialErrorCode.RequestNotFound, "Friend request does not exist.");
        }

        await socialRepository.RemoveFriendRequestAsync(request.RequestId, cancellationToken);
        return SocialResult.Success();
    }

    public async Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var friendships = await socialRepository.GetFriendshipsAsync(playerId, cancellationToken);
        var friends = new List<FriendResponse>();

        for (var i = 0; i < friendships.Count; i++)
        {
            var friendship = friendships[i];
            var friendPlayerId = string.Equals(friendship.PlayerAId, playerId, StringComparison.Ordinal)
                ? friendship.PlayerBId
                : friendship.PlayerAId;
            var profile = await accounts.GetProfileAsync(friendPlayerId, cancellationToken);
            if (profile == null)
            {
                continue;
            }

            friends.Add(new FriendResponse(profile, realtimeHub.IsConnected(profile.PlayerId)));
        }

        return friends;
    }

    public async Task<SocialResult> DeleteFriendAsync(
        string playerId,
        string friendPlayerId,
        CancellationToken cancellationToken)
    {
        friendPlayerId = friendPlayerId.Trim();
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return SocialResult.Fail(SocialErrorCode.Validation, "Friend player ID is required.");
        }

        var alreadyFriends = await socialRepository.AreFriendsAsync(
            playerId,
            friendPlayerId,
            cancellationToken);
        if (!alreadyFriends)
        {
            return SocialResult.Fail(SocialErrorCode.AccountNotFound, "Friendship does not exist.");
        }

        await socialRepository.RemoveFriendshipAsync(playerId, friendPlayerId, cancellationToken);
        await socialRepository.RemoveFriendRequestsBetweenAsync(playerId, friendPlayerId, cancellationToken);
        return SocialResult.Success();
    }
}
