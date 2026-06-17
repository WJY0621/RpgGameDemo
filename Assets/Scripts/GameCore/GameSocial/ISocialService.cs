using System.Collections.Generic;

public interface ISocialService
{
    List<FriendInfo> LoadFriends(string ownerPlayerId);
    FriendInfo SearchPlayerById(string requesterPlayerId, string targetPlayerId);
    List<FriendRequestData> LoadIncomingFriendRequests(string ownerPlayerId);
    bool SendFriendRequest(string requesterPlayerId, string requesterDisplayName, FriendInfo targetFriend);
    bool AcceptFriendRequest(string ownerPlayerId, string requestId);
    bool RefuseFriendRequest(string ownerPlayerId, string requestId);
    bool AddFriend(string ownerPlayerId, FriendInfo friendInfo);
    bool RemoveFriend(string ownerPlayerId, string friendPlayerId);
    void ClearFriends(string ownerPlayerId);
}
