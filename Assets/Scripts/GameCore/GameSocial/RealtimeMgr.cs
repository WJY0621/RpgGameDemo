using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class RealtimeMgr
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const int ReconnectDelayMilliseconds = 3000;
    private const int MaxReconnectDelayMilliseconds = 30000;

    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    private int consecutiveFailures;

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;
    public string LastError { get; private set; }

    public event Action<ChatMessageData> OnChatMessageReceived;
    public event Action<InviteData> OnOnlineInviteReceived;
    public event Action<InviteResultData> OnOnlineInviteResultReceived;
    public event Action OnSocialRefreshReceived;
    public event Action<string> OnSessionKickedReceived;

    public void Init()
    {
        Reconnect();
    }

    public void Reconnect()
    {
        Disconnect();

        string token = HttpSessionContext.GetSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        cts = new CancellationTokenSource();
        ConnectLoopAsync(token, cts.Token).Forget();
    }

    public void Disconnect()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        if (socket != null)
        {
            socket.Dispose();
            socket = null;
        }
    }

    private async UniTaskVoid ConnectLoopAsync(string token, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               string.Equals(token, HttpSessionContext.GetSessionToken(), StringComparison.Ordinal))
        {
            try
            {
                socket = new ClientWebSocket();
                Uri uri = BuildWebSocketUri(token);
                await socket.ConnectAsync(uri, cancellationToken);
                LastError = string.Empty;

                // 连接成功：如果之前失败过，提示一次恢复并清零计数
                if (consecutiveFailures > 0)
                {
                    Debug.Log("[RealtimeMgr] WebSocket 已重新连接。");
                    consecutiveFailures = 0;
                }

                await ReceiveLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                consecutiveFailures++;

                // 只在第一次失败时警告，避免服务器长时间不可用刷屏（仍会静默自动重试）
                if (consecutiveFailures == 1)
                {
                    Debug.LogWarning($"[RealtimeMgr] WebSocket 连接失败，将在后台自动重试（不再刷屏）：{LastError}");
                }
            }
            finally
            {
                if (socket != null)
                {
                    socket.Dispose();
                    socket = null;
                }
            }

            if (!cancellationToken.IsCancellationRequested &&
                string.Equals(token, HttpSessionContext.GetSessionToken(), StringComparison.Ordinal))
            {
                try
                {
                    // 退避：连不上时重试间隔随失败次数拉长（3s 起步，最多 30s）
                    int delay = Mathf.Min(
                        ReconnectDelayMilliseconds * Mathf.Max(1, consecutiveFailures),
                        MaxReconnectDelayMilliseconds);
                    await UniTask.Delay(delay, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async UniTask ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        StringBuilder builder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested &&
               socket != null &&
               socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            string json = builder.ToString();
            builder.Clear();
            await UniTask.SwitchToMainThread(cancellationToken);
            HandleEvent(json);
        }
    }

    private void HandleEvent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        RealtimeEventData eventData = JsonUtility.FromJson<RealtimeEventData>(json);
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.type))
        {
            return;
        }

        if (string.Equals(eventData.type, "chat.message", StringComparison.OrdinalIgnoreCase))
        {
            OnChatMessageReceived?.Invoke(ToChatMessageData(eventData));
        }
        else if (string.Equals(eventData.type, "online.invite", StringComparison.OrdinalIgnoreCase))
        {
            OnOnlineInviteReceived?.Invoke(ToInviteData(eventData));
        }
        else if (string.Equals(eventData.type, "online.invite-result", StringComparison.OrdinalIgnoreCase))
        {
            OnOnlineInviteResultReceived?.Invoke(ToInviteResultData(eventData));
        }
        else if (string.Equals(eventData.type, "social.refresh", StringComparison.OrdinalIgnoreCase))
        {
            OnSocialRefreshReceived?.Invoke();
        }
        else if (string.Equals(eventData.type, "session.kicked", StringComparison.OrdinalIgnoreCase))
        {
            OnSessionKickedReceived?.Invoke(eventData.reason);
        }
    }

    private static ChatMessageData ToChatMessageData(RealtimeEventData eventData)
    {
        return new ChatMessageData
        {
            messageId = eventData.messageId,
            channelType = ChatChannelType.FriendPrivate,
            senderPlayerId = eventData.senderPlayerId,
            receiverPlayerId = eventData.receiverPlayerId,
            messageText = eventData.messageText,
            sentAtUnixSeconds = ParseUnixSeconds(eventData.sentAt)
        };
    }

    private static InviteData ToInviteData(RealtimeEventData eventData)
    {
        return new InviteData
        {
            inviteId = eventData.inviteId,
            inviteType = string.Equals(eventData.inviteType, "RequestToJoinWorld", StringComparison.OrdinalIgnoreCase)
                ? InviteType.RequestToJoinWorld
                : InviteType.InviteToMyWorld,
            requesterPlayerId = eventData.requesterPlayerId,
            requesterDisplayName = eventData.requesterDisplayName,
            hostPlayerId = eventData.hostPlayerId,
            targetPlayerId = eventData.targetPlayerId,
            address = eventData.address,
            port = (ushort)Mathf.Clamp(eventData.port, 1, ushort.MaxValue),
            relayJoinCode = eventData.relayJoinCode,
            lobbyId = eventData.lobbyId,
            createdAtUnixSeconds = ParseUnixSeconds(eventData.createdAt)
        };
    }

    private static InviteResultData ToInviteResultData(RealtimeEventData eventData)
    {
        return new InviteResultData
        {
            inviteId = eventData.inviteId,
            inviteType = string.Equals(eventData.inviteType, "RequestToJoinWorld", StringComparison.OrdinalIgnoreCase)
                ? InviteType.RequestToJoinWorld
                : InviteType.InviteToMyWorld,
            requesterPlayerId = eventData.requesterPlayerId,
            requesterDisplayName = eventData.requesterDisplayName,
            hostPlayerId = eventData.hostPlayerId,
            targetPlayerId = eventData.targetPlayerId,
            address = eventData.address,
            port = (ushort)Mathf.Clamp(eventData.port, 1, ushort.MaxValue),
            relayJoinCode = eventData.relayJoinCode,
            lobbyId = eventData.lobbyId,
            accepted = eventData.accepted,
            respondedAtUnixSeconds = ParseUnixSeconds(eventData.respondedAt)
        };
    }

    private static long ParseUnixSeconds(string value)
    {
        if (DateTimeOffset.TryParse(value, out DateTimeOffset parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static Uri BuildWebSocketUri(string token)
    {
        string baseUrl = PlayerPrefs.GetString(BaseUrlKey, string.Empty);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        UriBuilder builder = new UriBuilder(baseUrl.TrimEnd('/'));
        builder.Scheme = string.Equals(builder.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        builder.Path = "ws";
        builder.Query = "token=" + Uri.EscapeDataString(token);
        return builder.Uri;
    }

#pragma warning disable 0649
    [Serializable]
    private class RealtimeEventData
    {
        public string type;
        public string messageId;
        public string senderPlayerId;
        public string receiverPlayerId;
        public string messageText;
        public string sentAt;
        public string inviteId;
        public string inviteType;
        public string requesterPlayerId;
        public string requesterDisplayName;
        public string hostPlayerId;
        public string targetPlayerId;
        public string address;
        public int port;
        public string relayJoinCode;
        public string lobbyId;
        public bool accepted;
        public string reason;
        public string respondedAt;
        public string createdAt;
    }
#pragma warning restore 0649
}
