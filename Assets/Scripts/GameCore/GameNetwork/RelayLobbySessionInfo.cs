public sealed class RelayLobbySessionInfo
{
    public bool success;
    public string relayJoinCode;
    public string lobbyId;
    public string error;

    public static RelayLobbySessionInfo Success(string relayJoinCode, string lobbyId)
    {
        return new RelayLobbySessionInfo
        {
            success = true,
            relayJoinCode = relayJoinCode ?? string.Empty,
            lobbyId = lobbyId ?? string.Empty,
            error = string.Empty
        };
    }

    public static RelayLobbySessionInfo Fail(string error)
    {
        return new RelayLobbySessionInfo
        {
            success = false,
            relayJoinCode = string.Empty,
            lobbyId = string.Empty,
            error = string.IsNullOrWhiteSpace(error) ? "Relay/Lobby operation failed." : error
        };
    }
}
