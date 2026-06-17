using System;

[Serializable]
public class FriendInfo
{
    public string playerId;
    public string displayName;
    public FriendOnlineState onlineState;
    public bool hasUnreadMessage;

    public FriendInfo Clone()
    {
        return new FriendInfo
        {
            playerId = playerId,
            displayName = displayName,
            onlineState = onlineState,
            hasUnreadMessage = hasUnreadMessage
        };
    }
}
