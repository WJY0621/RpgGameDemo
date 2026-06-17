using System;
using System.Collections.Generic;
using UnityEngine;

public class MockChatService : IChatService
{
    private const int MaxStoredMessages = 30;
    private const string ChatKeyPrefix = "WorkDemo.MockChat.Friend.";

    public List<ChatMessageData> LoadFriendMessages(string ownerPlayerId, string friendPlayerId)
    {
        ChatMessageListWrapper wrapper = LoadWrapper(ownerPlayerId, friendPlayerId);
        List<ChatMessageData> result = new List<ChatMessageData>();
        for (int i = 0; i < wrapper.messages.Count; i++)
        {
            ChatMessageData message = wrapper.messages[i];
            if (message != null)
            {
                result.Add(message.Clone());
            }
        }

        return result;
    }

    public ChatMessageData SendFriendMessage(string ownerPlayerId, string friendPlayerId, string messageText)
    {
        string safeText = SanitizeMessage(messageText);
        if (string.IsNullOrWhiteSpace(ownerPlayerId) ||
            string.IsNullOrWhiteSpace(friendPlayerId) ||
            string.IsNullOrWhiteSpace(safeText))
        {
            return null;
        }

        ChatMessageData message = new ChatMessageData
        {
            messageId = Guid.NewGuid().ToString("N"),
            channelType = ChatChannelType.FriendPrivate,
            senderPlayerId = ownerPlayerId,
            receiverPlayerId = friendPlayerId,
            messageText = safeText,
            sentAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        ChatMessageListWrapper wrapper = LoadWrapper(ownerPlayerId, friendPlayerId);
        wrapper.messages.Add(message);
        Trim(wrapper.messages);
        SaveWrapper(ownerPlayerId, friendPlayerId, wrapper);
        return message.Clone();
    }

    public int LoadUnreadMessageCount(string ownerPlayerId)
    {
        return 0;
    }

    public ChatUnreadSummaryData LoadUnreadSummary(string ownerPlayerId)
    {
        return new ChatUnreadSummaryData();
    }

    public void MarkFriendMessagesRead(string ownerPlayerId, string friendPlayerId)
    {
    }

    public void ClearFriendMessages(string ownerPlayerId, string friendPlayerId)
    {
        PlayerPrefs.DeleteKey(BuildChatKey(ownerPlayerId, friendPlayerId));
        PlayerPrefs.Save();
    }

    private ChatMessageListWrapper LoadWrapper(string ownerPlayerId, string friendPlayerId)
    {
        string json = PlayerPrefs.GetString(BuildChatKey(ownerPlayerId, friendPlayerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChatMessageListWrapper();
        }

        ChatMessageListWrapper wrapper = JsonUtility.FromJson<ChatMessageListWrapper>(json);
        if (wrapper == null)
        {
            wrapper = new ChatMessageListWrapper();
        }

        if (wrapper.messages == null)
        {
            wrapper.messages = new List<ChatMessageData>();
        }

        return wrapper;
    }

    private void SaveWrapper(string ownerPlayerId, string friendPlayerId, ChatMessageListWrapper wrapper)
    {
        PlayerPrefs.SetString(BuildChatKey(ownerPlayerId, friendPlayerId), JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    private string BuildChatKey(string ownerPlayerId, string friendPlayerId)
    {
        string a = NormalizePlayerId(ownerPlayerId);
        string b = NormalizePlayerId(friendPlayerId);
        return ChatKeyPrefix + a + "." + b;
    }

    private string NormalizePlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim().ToUpperInvariant();
    }

    private string SanitizeMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return string.Empty;
        }

        string trimmed = messageText.Trim();
        return trimmed.Length > 100 ? trimmed.Substring(0, 100) : trimmed;
    }

    private void Trim(List<ChatMessageData> messages)
    {
        while (messages.Count > MaxStoredMessages)
        {
            messages.RemoveAt(0);
        }
    }

    [Serializable]
    private class ChatMessageListWrapper
    {
        public List<ChatMessageData> messages = new List<ChatMessageData>();
    }
}
