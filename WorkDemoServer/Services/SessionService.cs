using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WorkDemoServer.Services;

public sealed class SessionService
{
    private readonly ConcurrentDictionary<string, string> playerIdsByToken = new();
    private readonly ConcurrentDictionary<string, string> tokensByPlayerId = new();

    public string CreateSession(string playerId)
    {
        InvalidatePlayerSessions(playerId);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        playerIdsByToken[token] = playerId;
        tokensByPlayerId[playerId] = token;
        return token;
    }

    public bool TryGetPlayerId(string token, out string playerId)
    {
        if (!playerIdsByToken.TryGetValue(token, out playerId!))
        {
            return false;
        }

        if (tokensByPlayerId.TryGetValue(playerId, out var activeToken) &&
            string.Equals(activeToken, token, StringComparison.Ordinal))
        {
            return true;
        }

        playerIdsByToken.TryRemove(token, out _);
        playerId = string.Empty;
        return false;
    }

    public bool IsOnline(string playerId)
    {
        return tokensByPlayerId.ContainsKey(playerId);
    }

    public void InvalidatePlayerSessions(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        if (tokensByPlayerId.TryRemove(playerId, out var activeToken))
        {
            playerIdsByToken.TryRemove(activeToken, out _);
        }

        foreach (var pair in playerIdsByToken.ToArray())
        {
            if (string.Equals(pair.Value, playerId, StringComparison.Ordinal))
            {
                playerIdsByToken.TryRemove(pair.Key, out _);
            }
        }
    }
}
