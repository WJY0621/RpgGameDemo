using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class SocialMgr
{
    private const string LocalTestAddress = "127.0.0.1";
    private const string LocalListenAddress = "0.0.0.0";
    private const ushort LocalTestPort = 7777;

    private readonly ISocialService socialService;
    private readonly HttpOnlineInviteService onlineInviteService = new HttpOnlineInviteService();
    private readonly List<FriendInfo> friends = new List<FriendInfo>();
    private readonly List<FriendRequestData> incomingFriendRequests = new List<FriendRequestData>();
    private readonly List<InviteData> incomingOnlineRequests = new List<InviteData>();
    private string pendingRelayJoinCode;
    private string pendingLobbyId;

    public IReadOnlyList<FriendInfo> Friends => friends;
    public IReadOnlyList<FriendRequestData> IncomingFriendRequests => incomingFriendRequests;
    public IReadOnlyList<InviteData> IncomingOnlineRequests => incomingOnlineRequests;
    public FriendInfo LastSearchResult { get; private set; }

    public event Action OnFriendsChanged;
    public event Action OnFriendRequestsChanged;
    public event Action OnOnlineRequestsChanged;
    public event Action<InviteData> OnOnlineRequestReceived;
    public event Action<InviteResultData> OnOnlineInviteResultReceived;
    public event Action<FriendInfo> OnSearchResultChanged;

    public SocialMgr(ISocialService socialService = null)
    {
        this.socialService = socialService ?? new MockSocialService();
    }

    public void Init()
    {
        SyncCurrentAccountProfileName();
        RefreshFriends();
        RefreshIncomingFriendRequests();
    }

    public void SyncCurrentAccountProfileName()
    {
        if (socialService is not MockSocialService mockSocialService)
        {
            return;
        }

        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        mockSocialService.SyncKnownProfile(profile.playerId, profile.displayName);
    }

    public void RefreshFriends(bool forceNotify = true)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            bool hadFriends = friends.Count > 0;
            friends.Clear();
            if (forceNotify || hadFriends)
            {
                OnFriendsChanged?.Invoke();
            }
            return;
        }

        List<FriendInfo> loadedFriends = socialService.LoadFriends(profile.playerId);
        bool changed = !AreFriendListsEqual(friends, loadedFriends);
        friends.Clear();
        friends.AddRange(loadedFriends);
        if (forceNotify || changed)
        {
            OnFriendsChanged?.Invoke();
        }
    }

    public void RefreshIncomingFriendRequests()
    {
        incomingFriendRequests.Clear();

        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            GameMgr.RedDot?.Clear(RedDotType.FriendRequest);
            OnFriendRequestsChanged?.Invoke();
            return;
        }

        List<FriendRequestData> loadedRequests = socialService.LoadIncomingFriendRequests(profile.playerId);
        incomingFriendRequests.AddRange(loadedRequests);
        GameMgr.RedDot?.SetCount(RedDotType.FriendRequest, incomingFriendRequests.Count);
        OnFriendRequestsChanged?.Invoke();
    }

    public FriendInfo SearchPlayerById(string targetPlayerId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        LastSearchResult = profile != null && profile.IsValid()
            ? socialService.SearchPlayerById(profile.playerId, targetPlayerId)
            : null;

        OnSearchResultChanged?.Invoke(LastSearchResult?.Clone());
        return LastSearchResult?.Clone();
    }

    public bool AddLastSearchResultAsFriend()
    {
        if (LastSearchResult == null)
        {
            return false;
        }

        return AddFriend(LastSearchResult);
    }

    public bool SendFriendRequest(FriendInfo targetFriend)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid() || targetFriend == null)
        {
            return false;
        }

        bool sent = socialService.SendFriendRequest(profile.playerId, profile.displayName, targetFriend);
        if (sent)
        {
            RefreshIncomingFriendRequests();
        }

        return sent;
    }

    public bool AcceptFriendRequest(string requestId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return false;
        }

        bool accepted = socialService.AcceptFriendRequest(profile.playerId, requestId);
        if (accepted)
        {
            RefreshIncomingFriendRequests();
            RefreshFriends();
        }

        return accepted;
    }

    public bool RefuseFriendRequest(string requestId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return false;
        }

        bool refused = socialService.RefuseFriendRequest(profile.playerId, requestId);
        if (refused)
        {
            RefreshIncomingFriendRequests();
        }

        return refused;
    }

    public bool AddFriend(FriendInfo friendInfo)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return false;
        }

        bool added = socialService.AddFriend(profile.playerId, friendInfo);
        if (added)
        {
            RefreshFriends();
        }

        return added;
    }

    public bool RemoveFriend(string friendPlayerId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return false;
        }

        bool removed = socialService.RemoveFriend(profile.playerId, friendPlayerId);
        if (removed)
        {
            RefreshFriends();
        }

        return removed;
    }

    public void ClearFriends()
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        socialService.ClearFriends(profile.playerId);
        RefreshFriends();
        RefreshIncomingFriendRequests();
    }

    public FriendInfo GetFriend(string friendPlayerId)
    {
        for (int i = 0; i < friends.Count; i++)
        {
            FriendInfo friend = friends[i];
            if (friend != null &&
                string.Equals(friend.playerId, friendPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return friend.Clone();
            }
        }

        return null;
    }

    public async UniTask<bool> SendOnlineRequestAsync(FriendInfo targetFriend, InviteType inviteType)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid() || targetFriend == null || string.IsNullOrWhiteSpace(targetFriend.playerId))
        {
            GameMgr.Message?.RegisterMessage("Online request failed: account or friend is invalid.", priority: MessagePriority.High);
            return false;
        }

        if (targetFriend.onlineState == FriendOnlineState.Offline)
        {
            GameMgr.Message?.RegisterMessage("Friend is offline, cannot send online request.", priority: MessagePriority.High);
            return false;
        }

        if (inviteType == InviteType.InviteToMyWorld)
        {
            bool hostStarted = await EnsureHostForOutgoingInviteAsync();
            if (!hostStarted)
            {
                return false;
            }
        }

        InviteData inviteData = CreateOnlineRequest(profile, targetFriend, inviteType);
        InviteData sentInvite = onlineInviteService.SendOnlineInvite(inviteData);
        if (sentInvite == null)
        {
            string error = string.IsNullOrWhiteSpace(onlineInviteService.LastError)
                ? "Online request failed."
                : onlineInviteService.LastError;
            GameMgr.Message?.RegisterMessage(error, priority: MessagePriority.High);
            return false;
        }

        string friendName = string.IsNullOrWhiteSpace(targetFriend.displayName) ? targetFriend.playerId : targetFriend.displayName;
        string actionText = inviteType == InviteType.RequestToJoinWorld ? "request to join" : "invite";
        GameMgr.Message?.RegisterMessage($"Sent {actionText} to {friendName}.", priority: MessagePriority.Medium);
        GameMgr.Message?.RegisterMessage("Waiting for invite response.", priority: MessagePriority.Medium);
        return true;
    }

    public void ReceiveOnlineRequest(InviteData inviteData)
    {
        if (inviteData == null || string.IsNullOrWhiteSpace(inviteData.inviteId))
        {
            return;
        }

        InviteData savedInvite = inviteData.Clone();
        RemoveIncomingOnlineRequest(savedInvite.inviteId);
        incomingOnlineRequests.Add(savedInvite);
        OnOnlineRequestsChanged?.Invoke();
        OnOnlineRequestReceived?.Invoke(savedInvite.Clone());
        OpenOnlineRequestPanelAsync(savedInvite).Forget();
    }

    public bool RefuseOnlineRequest(InviteData inviteData)
    {
        if (inviteData == null)
        {
            return false;
        }

        SendOnlineInviteResult(inviteData, false, LocalTestAddress, inviteData.port);
        bool removed = RemoveIncomingOnlineRequest(inviteData.inviteId);
        if (removed)
        {
            GameMgr.Message?.RegisterMessage("Online request refused.", priority: MessagePriority.Medium);
            OnOnlineRequestsChanged?.Invoke();
        }

        return removed;
    }

    public async UniTask<bool> AcceptOnlineRequestAsync(InviteData inviteData)
    {
        if (inviteData == null || GameMgr.Network == null)
        {
            return false;
        }

        bool success;
        if (inviteData.inviteType == InviteType.RequestToJoinWorld)
        {
            success = await EnsureHostForOutgoingInviteAsync();
            if (success && SendOnlineInviteResult(inviteData, true, LocalTestAddress, inviteData.port) == null)
            {
                return false;
            }
        }
        else
        {
            InviteResultData response = SendOnlineInviteResult(inviteData, true, inviteData.address, inviteData.port);
            if (response == null)
            {
                return false;
            }

            success = await StartClientForInviteAsync(inviteData.relayJoinCode, inviteData.lobbyId, inviteData.address, inviteData.port);
        }

        if (!success)
        {
            GameMgr.Message?.RegisterMessage(GameMgr.Network.LastError, priority: MessagePriority.High);
            return false;
        }

        RemoveIncomingOnlineRequest(inviteData.inviteId);
        OnOnlineRequestsChanged?.Invoke();
        GameMgr.Message?.RegisterMessage("Online request accepted.", priority: MessagePriority.Medium);
        return true;
    }

    public void ReceiveOnlineInviteResult(InviteResultData resultData)
    {
        if (resultData == null || string.IsNullOrWhiteSpace(resultData.inviteId))
        {
            return;
        }

        OnOnlineInviteResultReceived?.Invoke(resultData.Clone());
        HandleOnlineInviteResultAsync(resultData.Clone()).Forget();
    }

    public void HandleRealtimeSocialRefresh()
    {
        RefreshIncomingFriendRequests();
        RefreshFriends(false);
    }

    private InviteData CreateOnlineRequest(AccountProfile profile, FriendInfo targetFriend, InviteType inviteType)
    {
        string myPlayerId = profile != null ? profile.playerId : string.Empty;
        string myDisplayName = profile != null ? profile.displayName : string.Empty;
        string friendPlayerId = targetFriend != null ? targetFriend.playerId : string.Empty;

        return new InviteData
        {
            inviteId = Guid.NewGuid().ToString("N"),
            inviteType = inviteType,
            requesterPlayerId = myPlayerId,
            requesterDisplayName = myDisplayName,
            hostPlayerId = inviteType == InviteType.InviteToMyWorld ? myPlayerId : friendPlayerId,
            targetPlayerId = friendPlayerId,
            address = LocalTestAddress,
            port = LocalTestPort,
            relayJoinCode = pendingRelayJoinCode,
            lobbyId = pendingLobbyId,
            createdAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private async UniTask<bool> EnsureHostForOutgoingInviteAsync()
    {
        if (GameMgr.Network == null)
        {
            return false;
        }

        if (GameMgr.Network.IsSessionActive)
        {
            if (GameMgr.Network.IsServer)
            {
                RelayLobbySessionInfo activeRelaySession = GameMgr.Network.LastRelayHostSession;
                pendingRelayJoinCode = activeRelaySession != null ? activeRelaySession.relayJoinCode : string.Empty;
                pendingLobbyId = activeRelaySession != null ? activeRelaySession.lobbyId : string.Empty;
                return true;
            }

            GameMgr.Message?.RegisterMessage("Already connected as client, cannot invite friend to this world.", priority: MessagePriority.High);
            return false;
        }

        pendingRelayJoinCode = string.Empty;
        pendingLobbyId = string.Empty;

        bool success = WorkDemoRelayLobbyService.IsAvailable
            ? await GameMgr.Network.StartRelayHostAsync()
            : await GameMgr.Network.StartHostAsync(LocalListenAddress, LocalTestPort);
        if (!success)
        {
            GameMgr.Message?.RegisterMessage(GameMgr.Network.LastError, priority: MessagePriority.High);
            return false;
        }

        RelayLobbySessionInfo relaySession = GameMgr.Network.LastRelayHostSession;
        if (relaySession != null && relaySession.success)
        {
            pendingRelayJoinCode = relaySession.relayJoinCode;
            pendingLobbyId = relaySession.lobbyId;
        }

        return success;
    }

    private InviteResultData SendOnlineInviteResult(InviteData inviteData, bool accepted, string address, ushort port)
    {
        InviteResultData result = onlineInviteService.SendOnlineInviteResult(
            inviteData,
            accepted,
            address,
            port,
            pendingRelayJoinCode,
            pendingLobbyId);
        if (result == null)
        {
            string error = string.IsNullOrWhiteSpace(onlineInviteService.LastError)
                ? "Online invite response failed."
                : onlineInviteService.LastError;
            GameMgr.Message?.RegisterMessage(error, priority: MessagePriority.High);
        }

        return result;
    }

    private async UniTaskVoid HandleOnlineInviteResultAsync(InviteResultData resultData)
    {
        string targetName = ResolveFriendDisplayName(resultData.targetPlayerId);
        if (!resultData.accepted)
        {
            GameMgr.Message?.RegisterMessage($"{targetName} refused the online request.", priority: MessagePriority.Medium);
            return;
        }

        GameMgr.Message?.RegisterMessage($"{targetName} accepted the online request.", priority: MessagePriority.Medium);
        if (resultData.inviteType != InviteType.RequestToJoinWorld)
        {
            return;
        }

        if (GameMgr.Network == null)
        {
            return;
        }

        bool success = await StartClientForInviteAsync(
            resultData.relayJoinCode,
            resultData.lobbyId,
            resultData.address,
            resultData.port);
        if (!success)
        {
            GameMgr.Message?.RegisterMessage(GameMgr.Network.LastError, priority: MessagePriority.High);
        }
    }

    private async UniTask<bool> StartClientForInviteAsync(string relayJoinCode, string lobbyId, string address, ushort port)
    {
        if (!string.IsNullOrWhiteSpace(relayJoinCode) && WorkDemoRelayLobbyService.IsAvailable)
        {
            return await GameMgr.Network.StartRelayClientAsync(relayJoinCode, lobbyId);
        }

        return await GameMgr.Network.StartClientAsync(address, port);
    }

    private string ResolveFriendDisplayName(string friendPlayerId)
    {
        FriendInfo friend = GetFriend(friendPlayerId);
        if (friend != null && !string.IsNullOrWhiteSpace(friend.displayName))
        {
            return friend.displayName;
        }

        return string.IsNullOrWhiteSpace(friendPlayerId) ? "Friend" : friendPlayerId;
    }

    private bool RemoveIncomingOnlineRequest(string inviteId)
    {
        if (string.IsNullOrWhiteSpace(inviteId))
        {
            return false;
        }

        int removedCount = incomingOnlineRequests.RemoveAll(invite =>
            invite != null &&
            string.Equals(invite.inviteId, inviteId, StringComparison.OrdinalIgnoreCase));

        return removedCount > 0;
    }

    private bool AreFriendListsEqual(IReadOnlyList<FriendInfo> left, IReadOnlyList<FriendInfo> right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            FriendInfo leftFriend = left[i];
            FriendInfo rightFriend = right[i];
            if (leftFriend == null || rightFriend == null)
            {
                if (leftFriend != rightFriend)
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(leftFriend.playerId, rightFriend.playerId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(leftFriend.displayName, rightFriend.displayName, StringComparison.Ordinal) ||
                leftFriend.onlineState != rightFriend.onlineState)
            {
                return false;
            }
        }

        return true;
    }

    private async UniTaskVoid OpenOnlineRequestPanelAsync(InviteData inviteData)
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        OnlineRequestPanel panel = await GameMgr.UI.ShowPanel<OnlineRequestPanel>();
        panel?.Open(inviteData);
    }
}
