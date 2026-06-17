using System;

[Serializable]
public class FriendRequestData
{
    public string requestId;
    public string fromPlayerId;
    public string fromDisplayName;
    public string toPlayerId;
    public long createdAtUnixSeconds;

    public FriendRequestData Clone()
    {
        return new FriendRequestData
        {
            requestId = requestId,
            fromPlayerId = fromPlayerId,
            fromDisplayName = fromDisplayName,
            toPlayerId = toPlayerId,
            createdAtUnixSeconds = createdAtUnixSeconds
        };
    }
}
