using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public class ChatMgr
{
    private const int UnreadRefreshIntervalMilliseconds = 3000;

    private readonly IChatService chatService;
    private readonly Dictionary<string, int> friendUnreadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource unreadRefreshCts;
    private int unreadMessageCount;

    public event Action<string> OnFriendConversationChanged;
    public event Action<int> OnUnreadMessageCountChanged;

    public ChatMgr(IChatService chatService = null)
    {
        this.chatService = chatService ?? new MockChatService();
    }

    public int UnreadMessageCount => unreadMessageCount;
    public IReadOnlyDictionary<string, int> FriendUnreadCounts => friendUnreadCounts;

    public void Init()
    {
        StartUnreadRefreshLoop();
        RefreshUnreadCount();
    }

    public List<ChatMessageData> LoadFriendMessages(string friendPlayerId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return new List<ChatMessageData>();
        }

        List<ChatMessageData> messages = chatService.LoadFriendMessages(profile.playerId, friendPlayerId);
        chatService.MarkFriendMessagesRead(profile.playerId, friendPlayerId);
        RefreshUnreadCount(false);
        return messages;
    }

    public ChatMessageData SendFriendMessage(string friendPlayerId, string messageText)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return null;
        }

        ChatMessageData sentMessage = chatService.SendFriendMessage(profile.playerId, friendPlayerId, messageText);
        if (sentMessage != null)
        {
            OnFriendConversationChanged?.Invoke(friendPlayerId);
            RefreshUnreadCount(false);
        }

        return sentMessage;
    }

    public void RefreshUnreadCount(bool notifyWhenUnchanged = true)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        ChatUnreadSummaryData summary = new ChatUnreadSummaryData();
        if (profile != null && profile.IsValid())
        {
            summary = chatService.LoadUnreadSummary(profile.playerId) ?? new ChatUnreadSummaryData();
        }

        bool changed = ApplyUnreadSummary(summary);
        if (!changed && !notifyWhenUnchanged)
        {
            return;
        }

        GameMgr.RedDot?.SetCount(RedDotType.Chat, unreadMessageCount);
        OnUnreadMessageCountChanged?.Invoke(unreadMessageCount);
    }

    public int GetFriendUnreadCount(string friendPlayerId)
    {
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return 0;
        }

        return friendUnreadCounts.TryGetValue(friendPlayerId, out int count) ? count : 0;
    }

    public bool HasFriendUnread(string friendPlayerId)
    {
        return GetFriendUnreadCount(friendPlayerId) > 0;
    }

    public void ClearFriendMessages(string friendPlayerId)
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        chatService.ClearFriendMessages(profile.playerId, friendPlayerId);
        RefreshUnreadCount(false);
        OnFriendConversationChanged?.Invoke(friendPlayerId);
    }

    public void HandleRealtimeMessage(ChatMessageData message)
    {
        if (message == null)
        {
            return;
        }

        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        string friendPlayerId = string.Equals(message.senderPlayerId, profile.playerId, StringComparison.OrdinalIgnoreCase)
            ? message.receiverPlayerId
            : message.senderPlayerId;

        RefreshUnreadCount(false);
        OnFriendConversationChanged?.Invoke(friendPlayerId);
    }

    private void StartUnreadRefreshLoop()
    {
        StopUnreadRefreshLoop();
        unreadRefreshCts = new CancellationTokenSource();
        RefreshUnreadLoopAsync(unreadRefreshCts.Token).Forget();
    }

    private void StopUnreadRefreshLoop()
    {
        if (unreadRefreshCts == null)
        {
            return;
        }

        unreadRefreshCts.Cancel();
        unreadRefreshCts.Dispose();
        unreadRefreshCts = null;
    }

    private async UniTaskVoid RefreshUnreadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool canceled = await UniTask.Delay(
                UnreadRefreshIntervalMilliseconds,
                cancellationToken: cancellationToken).SuppressCancellationThrow();
            if (canceled || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RefreshUnreadCount(false);
        }
    }

    private bool ApplyUnreadSummary(ChatUnreadSummaryData summary)
    {
        int nextTotal = Math.Max(0, summary != null ? summary.totalUnreadCount : 0);
        Dictionary<string, int> nextFriendCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (summary?.friends != null)
        {
            for (int i = 0; i < summary.friends.Count; i++)
            {
                FriendChatUnreadData item = summary.friends[i];
                if (item == null || string.IsNullOrWhiteSpace(item.friendPlayerId))
                {
                    continue;
                }

                int safeCount = Math.Max(0, item.unreadCount);
                if (safeCount > 0)
                {
                    nextFriendCounts[item.friendPlayerId] = safeCount;
                }
            }
        }

        bool changed = unreadMessageCount != nextTotal ||
                       !AreFriendUnreadCountsEqual(friendUnreadCounts, nextFriendCounts);

        unreadMessageCount = nextTotal;
        friendUnreadCounts.Clear();
        foreach (KeyValuePair<string, int> pair in nextFriendCounts)
        {
            friendUnreadCounts[pair.Key] = pair.Value;
        }

        return changed;
    }

    private static bool AreFriendUnreadCountsEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, int> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out int rightCount) || rightCount != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
