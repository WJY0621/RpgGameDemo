using System;

[Serializable]
public class InviteData
{
    public string inviteId;
    public InviteType inviteType;
    public string requesterPlayerId;
    public string requesterDisplayName;
    public string hostPlayerId;
    public string targetPlayerId;
    public string address;
    public ushort port;
    public string relayJoinCode;
    public string lobbyId;
    public long createdAtUnixSeconds;

    public InviteData Clone()
    {
        return new InviteData
        {
            inviteId = inviteId,
            inviteType = inviteType,
            requesterPlayerId = requesterPlayerId,
            requesterDisplayName = requesterDisplayName,
            hostPlayerId = hostPlayerId,
            targetPlayerId = targetPlayerId,
            address = address,
            port = port,
            relayJoinCode = relayJoinCode,
            lobbyId = lobbyId,
            createdAtUnixSeconds = createdAtUnixSeconds
        };
    }
}
