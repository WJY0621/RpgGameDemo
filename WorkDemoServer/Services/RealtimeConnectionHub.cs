using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

public sealed class RealtimeConnectionHub
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RealtimeConnection>> socketsByPlayer = new();

    public bool IsConnected(string playerId)
    {
        return socketsByPlayer.TryGetValue(playerId, out var sockets) &&
               sockets.Values.Any(connection => connection.Socket.State == WebSocketState.Open);
    }

    public async Task HandleSocketAsync(
        string playerId,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var socketId = Guid.NewGuid().ToString("N");
        var sockets = socketsByPlayer.GetOrAdd(playerId, _ => new ConcurrentDictionary<string, RealtimeConnection>());
        var connection = new RealtimeConnection(socket);
        sockets[socketId] = connection;

        try
        {
            await SendConnectionAsync(connection, new { type = "ready" }, cancellationToken);

            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (IsExpectedSocketException(exception))
        {
            // The client can close the game, reload, or lose the socket at any moment.
            // Treat those cases as a normal disconnect instead of surfacing them to Kestrel.
        }
        finally
        {
            RemoveConnection(playerId, socketId, connection);
            await CloseQuietlyAsync(socket);
        }
    }

    public Task SendChatMessageAsync(
        string targetPlayerId,
        ChatMessageResponse message,
        CancellationToken cancellationToken)
    {
        return SendToPlayerAsync(targetPlayerId, new
        {
            type = "chat.message",
            messageId = message.MessageId,
            senderPlayerId = message.SenderPlayerId,
            receiverPlayerId = message.ReceiverPlayerId,
            messageText = message.MessageText,
            sentAt = message.SentAt
        }, cancellationToken);
    }

    public Task SendOnlineInviteAsync(
        string targetPlayerId,
        OnlineInviteResponse invite,
        CancellationToken cancellationToken)
    {
        return SendToPlayerAsync(targetPlayerId, new
        {
            type = "online.invite",
            inviteId = invite.InviteId,
            inviteType = invite.InviteType,
            requesterPlayerId = invite.RequesterPlayerId,
            requesterDisplayName = invite.RequesterDisplayName,
            hostPlayerId = invite.HostPlayerId,
            targetPlayerId = invite.TargetPlayerId,
            address = invite.Address,
            port = invite.Port,
            relayJoinCode = invite.RelayJoinCode,
            lobbyId = invite.LobbyId,
            createdAt = invite.CreatedAt
        }, cancellationToken);
    }

    public Task SendOnlineInviteResultAsync(
        string targetPlayerId,
        OnlineInviteResultResponse result,
        CancellationToken cancellationToken)
    {
        return SendToPlayerAsync(targetPlayerId, new
        {
            type = "online.invite-result",
            inviteId = result.InviteId,
            inviteType = result.InviteType,
            requesterPlayerId = result.RequesterPlayerId,
            requesterDisplayName = result.RequesterDisplayName,
            hostPlayerId = result.HostPlayerId,
            targetPlayerId = result.TargetPlayerId,
            address = result.Address,
            port = result.Port,
            relayJoinCode = result.RelayJoinCode,
            lobbyId = result.LobbyId,
            accepted = result.Accepted,
            respondedAt = result.RespondedAt
        }, cancellationToken);
    }

    public Task BroadcastSocialRefreshAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        return BroadcastAsync(new
        {
            type = "social.refresh",
            reason
        }, cancellationToken);
    }

    public async Task DisconnectPlayerAsync(
        string playerId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!socketsByPlayer.TryRemove(playerId, out var sockets))
        {
            return;
        }

        foreach (var pair in sockets.ToArray())
        {
            var connection = pair.Value;
            var socket = connection.Socket;

            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await SendConnectionAsync(connection, new
                    {
                        type = "session.kicked",
                        reason
                    }, cancellationToken);
                }
            }
            catch (Exception exception) when (IsExpectedSocketException(exception))
            {
            }

            await CloseQuietlyAsync(socket);
        }
    }

    private async Task BroadcastAsync(
        object payload,
        CancellationToken cancellationToken)
    {
        foreach (var playerSockets in socketsByPlayer.ToArray())
        {
            await SendToPlayerAsync(playerSockets.Key, payload, cancellationToken);
        }
    }

    private async Task SendToPlayerAsync(
        string playerId,
        object payload,
        CancellationToken cancellationToken)
    {
        if (!socketsByPlayer.TryGetValue(playerId, out var sockets))
        {
            return;
        }

        foreach (var pair in sockets.ToArray())
        {
            var connection = pair.Value;
            var socket = connection.Socket;
            if (socket.State != WebSocketState.Open)
            {
                RemoveConnection(playerId, pair.Key, connection);
                continue;
            }

            try
            {
                await SendConnectionAsync(connection, payload, cancellationToken);
            }
            catch (Exception exception) when (IsExpectedSocketException(exception))
            {
                RemoveConnection(playerId, pair.Key, connection);
                await CloseQuietlyAsync(socket);
            }
        }
    }

    private void RemoveConnection(
        string playerId,
        string socketId,
        RealtimeConnection connection)
    {
        if (!socketsByPlayer.TryGetValue(playerId, out var sockets))
        {
            return;
        }

        if (sockets.TryRemove(socketId, out _) && sockets.IsEmpty)
        {
            socketsByPlayer.TryRemove(playerId, out _);
        }
    }

    private static async Task SendConnectionAsync(
        RealtimeConnection connection,
        object payload,
        CancellationToken cancellationToken)
    {
        await connection.SendLock.WaitAsync(cancellationToken);
        try
        {
            await SendAsync(connection.Socket, payload, cancellationToken);
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private static Task SendAsync(
        WebSocket socket,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(
            bytes,
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private static async Task CloseQuietlyAsync(WebSocket socket)
    {
        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch (Exception exception) when (IsExpectedSocketException(exception))
        {
        }
    }

    private static bool IsExpectedSocketException(Exception exception)
    {
        return exception is OperationCanceledException ||
               exception is WebSocketException ||
               exception is ObjectDisposedException ||
               exception is InvalidOperationException ||
               exception is IOException;
    }

    private sealed class RealtimeConnection
    {
        public RealtimeConnection(WebSocket socket)
        {
            Socket = socket;
        }

        public WebSocket Socket { get; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}
