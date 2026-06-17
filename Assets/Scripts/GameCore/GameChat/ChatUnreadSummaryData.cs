using System;
using System.Collections.Generic;

[Serializable]
public class ChatUnreadSummaryData
{
    public int totalUnreadCount;
    public List<FriendChatUnreadData> friends = new List<FriendChatUnreadData>();

    public ChatUnreadSummaryData Clone()
    {
        ChatUnreadSummaryData clone = new ChatUnreadSummaryData
        {
            totalUnreadCount = totalUnreadCount
        };

        if (friends != null)
        {
            for (int i = 0; i < friends.Count; i++)
            {
                FriendChatUnreadData item = friends[i];
                if (item != null)
                {
                    clone.friends.Add(item.Clone());
                }
            }
        }

        return clone;
    }
}

[Serializable]
public class FriendChatUnreadData
{
    public string friendPlayerId;
    public int unreadCount;

    public FriendChatUnreadData Clone()
    {
        return new FriendChatUnreadData
        {
            friendPlayerId = friendPlayerId,
            unreadCount = unreadCount
        };
    }
}
