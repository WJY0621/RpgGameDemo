using System.Collections.Generic;
using UnityEngine;

public class MockSocialService : ISocialService
{
    private const string FriendListKeyPrefix = "WorkDemo.MockSocial.Friends.";
    private const string FriendRequestKeyPrefix = "WorkDemo.MockSocial.Requests.";
    private const string ProfileNameKeyPrefix = "WorkDemo.MockSocial.ProfileName.";

    public void SyncKnownProfile(string playerId, string displayName)
    {
        SaveKnownProfileName(playerId, displayName);
    }

    public List<FriendInfo> LoadFriends(string ownerPlayerId)
    {
        FriendListWrapper wrapper = LoadWrapper(ownerPlayerId);
        List<FriendInfo> result = new List<FriendInfo>();
        for (int i = 0; i < wrapper.friends.Count; i++)
        {
            FriendInfo friend = wrapper.friends[i];
            if (friend != null && !string.IsNullOrWhiteSpace(friend.playerId))
            {
                FriendInfo savedFriend = friend.Clone();
                savedFriend.displayName = ResolveDisplayName(savedFriend.playerId, savedFriend.displayName);
                result.Add(savedFriend);
            }
        }

        return result;
    }

    public FriendInfo SearchPlayerById(string requesterPlayerId, string targetPlayerId)
    {
        string normalizedTargetId = NormalizePlayerId(targetPlayerId);
        if (string.IsNullOrWhiteSpace(normalizedTargetId))
        {
            return null;
        }

        if (string.Equals(NormalizePlayerId(requesterPlayerId), normalizedTargetId, System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new FriendInfo
        {
            playerId = normalizedTargetId,
            displayName = ResolveDisplayName(normalizedTargetId),
            onlineState = FriendOnlineState.Online
        };
    }

    public List<FriendRequestData> LoadIncomingFriendRequests(string ownerPlayerId)
    {
        FriendRequestListWrapper wrapper = LoadRequestWrapper(ownerPlayerId);
        List<FriendRequestData> result = new List<FriendRequestData>();
        for (int i = 0; i < wrapper.requests.Count; i++)
        {
            FriendRequestData request = wrapper.requests[i];
            if (request != null && !string.IsNullOrWhiteSpace(request.requestId))
            {
                result.Add(request.Clone());
            }
        }

        return result;
    }

    public bool SendFriendRequest(string requesterPlayerId, string requesterDisplayName, FriendInfo targetFriend)
    {
        if (string.IsNullOrWhiteSpace(requesterPlayerId) ||
            targetFriend == null ||
            string.IsNullOrWhiteSpace(targetFriend.playerId))
        {
            return false;
        }

        string fromPlayerId = NormalizePlayerId(requesterPlayerId);
        string targetPlayerId = NormalizePlayerId(targetFriend.playerId);
        SaveKnownProfileName(fromPlayerId, requesterDisplayName);
        SaveKnownProfileName(targetPlayerId, targetFriend.displayName);
        if (string.Equals(fromPlayerId, targetPlayerId, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsFriend(fromPlayerId, targetPlayerId))
        {
            return false;
        }

        FriendRequestListWrapper wrapper = LoadRequestWrapper(targetPlayerId);
        string requestId = BuildRequestId(fromPlayerId, targetPlayerId);
        for (int i = 0; i < wrapper.requests.Count; i++)
        {
            if (wrapper.requests[i] != null &&
                string.Equals(wrapper.requests[i].requestId, requestId, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        wrapper.requests.Add(new FriendRequestData
        {
            requestId = requestId,
            fromPlayerId = fromPlayerId,
            fromDisplayName = ResolveDisplayName(fromPlayerId, requesterDisplayName),
            toPlayerId = targetPlayerId,
            createdAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        SaveRequestWrapper(targetPlayerId, wrapper);
        return true;
    }

    public bool AcceptFriendRequest(string ownerPlayerId, string requestId)
    {
        FriendRequestListWrapper wrapper = LoadRequestWrapper(ownerPlayerId);
        int requestIndex = FindRequestIndex(wrapper.requests, requestId);
        if (requestIndex < 0)
        {
            return false;
        }

        FriendRequestData request = wrapper.requests[requestIndex];
        wrapper.requests.RemoveAt(requestIndex);
        SaveRequestWrapper(ownerPlayerId, wrapper);

        if (request == null || string.IsNullOrWhiteSpace(request.fromPlayerId))
        {
            return false;
        }

        AddFriend(ownerPlayerId, new FriendInfo
        {
            playerId = request.fromPlayerId,
            displayName = string.IsNullOrWhiteSpace(request.fromDisplayName) ? request.fromPlayerId : request.fromDisplayName,
            onlineState = FriendOnlineState.Online
        });

        string ownerDisplayName = ResolveDisplayName(ownerPlayerId);
        AccountProfile ownerProfile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (ownerProfile != null &&
            string.Equals(NormalizePlayerId(ownerProfile.playerId), NormalizePlayerId(ownerPlayerId), System.StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(ownerProfile.displayName))
        {
            ownerDisplayName = ownerProfile.displayName;
            SaveKnownProfileName(ownerPlayerId, ownerDisplayName);
        }

        AddFriend(request.fromPlayerId, new FriendInfo
        {
            playerId = ownerPlayerId,
            displayName = ownerDisplayName,
            onlineState = FriendOnlineState.Online
        });

        return true;
    }

    public bool RefuseFriendRequest(string ownerPlayerId, string requestId)
    {
        FriendRequestListWrapper wrapper = LoadRequestWrapper(ownerPlayerId);
        int requestIndex = FindRequestIndex(wrapper.requests, requestId);
        if (requestIndex < 0)
        {
            return false;
        }

        wrapper.requests.RemoveAt(requestIndex);
        SaveRequestWrapper(ownerPlayerId, wrapper);
        return true;
    }

    public bool AddFriend(string ownerPlayerId, FriendInfo friendInfo)
    {
        if (string.IsNullOrWhiteSpace(ownerPlayerId) ||
            friendInfo == null ||
            string.IsNullOrWhiteSpace(friendInfo.playerId))
        {
            return false;
        }

        FriendListWrapper wrapper = LoadWrapper(ownerPlayerId);
        string normalizedFriendId = NormalizePlayerId(friendInfo.playerId);
        for (int i = 0; i < wrapper.friends.Count; i++)
        {
            if (string.Equals(wrapper.friends[i].playerId, normalizedFriendId, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        FriendInfo savedFriend = friendInfo.Clone();
        savedFriend.playerId = normalizedFriendId;
        savedFriend.displayName = ResolveDisplayName(normalizedFriendId, savedFriend.displayName);
        wrapper.friends.Add(savedFriend);
        SaveWrapper(ownerPlayerId, wrapper);
        return true;
    }

    private bool IsFriend(string ownerPlayerId, string friendPlayerId)
    {
        FriendListWrapper wrapper = LoadWrapper(ownerPlayerId);
        string normalizedFriendId = NormalizePlayerId(friendPlayerId);
        for (int i = 0; i < wrapper.friends.Count; i++)
        {
            FriendInfo friend = wrapper.friends[i];
            if (friend != null &&
                string.Equals(friend.playerId, normalizedFriendId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool RemoveFriend(string ownerPlayerId, string friendPlayerId)
    {
        FriendListWrapper wrapper = LoadWrapper(ownerPlayerId);
        string normalizedFriendId = NormalizePlayerId(friendPlayerId);
        int removedCount = wrapper.friends.RemoveAll(friend =>
            friend != null &&
            string.Equals(friend.playerId, normalizedFriendId, System.StringComparison.OrdinalIgnoreCase));

        if (removedCount <= 0)
        {
            return false;
        }

        SaveWrapper(ownerPlayerId, wrapper);
        return true;
    }

    public void ClearFriends(string ownerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(ownerPlayerId))
        {
            return;
        }

        PlayerPrefs.DeleteKey(BuildFriendListKey(ownerPlayerId));
        PlayerPrefs.Save();
    }

    private FriendListWrapper LoadWrapper(string ownerPlayerId)
    {
        string json = PlayerPrefs.GetString(BuildFriendListKey(ownerPlayerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FriendListWrapper();
        }

        FriendListWrapper wrapper = JsonUtility.FromJson<FriendListWrapper>(json);
        if (wrapper == null)
        {
            wrapper = new FriendListWrapper();
        }

        if (wrapper.friends == null)
        {
            wrapper.friends = new List<FriendInfo>();
        }

        return wrapper;
    }

    private void SaveWrapper(string ownerPlayerId, FriendListWrapper wrapper)
    {
        PlayerPrefs.SetString(BuildFriendListKey(ownerPlayerId), JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    private FriendRequestListWrapper LoadRequestWrapper(string ownerPlayerId)
    {
        string json = PlayerPrefs.GetString(BuildFriendRequestKey(ownerPlayerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FriendRequestListWrapper();
        }

        FriendRequestListWrapper wrapper = JsonUtility.FromJson<FriendRequestListWrapper>(json);
        if (wrapper == null)
        {
            wrapper = new FriendRequestListWrapper();
        }

        if (wrapper.requests == null)
        {
            wrapper.requests = new List<FriendRequestData>();
        }

        return wrapper;
    }

    private void SaveRequestWrapper(string ownerPlayerId, FriendRequestListWrapper wrapper)
    {
        PlayerPrefs.SetString(BuildFriendRequestKey(ownerPlayerId), JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    private string BuildFriendListKey(string ownerPlayerId)
    {
        return FriendListKeyPrefix + NormalizePlayerId(ownerPlayerId);
    }

    private string BuildFriendRequestKey(string ownerPlayerId)
    {
        return FriendRequestKeyPrefix + NormalizePlayerId(ownerPlayerId);
    }

    private string BuildProfileNameKey(string playerId)
    {
        return ProfileNameKeyPrefix + NormalizePlayerId(playerId);
    }

    private void SaveKnownProfileName(string playerId, string displayName)
    {
        string normalizedPlayerId = NormalizePlayerId(playerId);
        if (string.IsNullOrWhiteSpace(normalizedPlayerId) || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        PlayerPrefs.SetString(BuildProfileNameKey(normalizedPlayerId), displayName.Trim());
        PlayerPrefs.Save();
    }

    private string ResolveDisplayName(string playerId, string fallback = null)
    {
        string normalizedPlayerId = NormalizePlayerId(playerId);
        if (!string.IsNullOrWhiteSpace(normalizedPlayerId))
        {
            string savedName = PlayerPrefs.GetString(BuildProfileNameKey(normalizedPlayerId), string.Empty);
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                return savedName;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallback) && !fallback.StartsWith("Player_", System.StringComparison.OrdinalIgnoreCase))
        {
            return fallback.Trim();
        }

        return string.IsNullOrWhiteSpace(normalizedPlayerId) ? string.Empty : normalizedPlayerId;
    }

    private string BuildRequestId(string fromPlayerId, string toPlayerId)
    {
        return NormalizePlayerId(fromPlayerId) + "_TO_" + NormalizePlayerId(toPlayerId);
    }

    private int FindRequestIndex(List<FriendRequestData> requests, string requestId)
    {
        string normalizedRequestId = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId.Trim().ToUpperInvariant();
        for (int i = 0; i < requests.Count; i++)
        {
            FriendRequestData request = requests[i];
            if (request != null &&
                string.Equals(request.requestId, normalizedRequestId, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private string NormalizePlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim().ToUpperInvariant();
    }

    [System.Serializable]
    private class FriendListWrapper
    {
        public List<FriendInfo> friends = new List<FriendInfo>();
    }

    [System.Serializable]
    private class FriendRequestListWrapper
    {
        public List<FriendRequestData> requests = new List<FriendRequestData>();
    }
}
