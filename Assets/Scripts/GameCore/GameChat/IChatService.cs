using System.Collections.Generic;

public interface IChatService
{
    List<ChatMessageData> LoadFriendMessages(string ownerPlayerId, string friendPlayerId);
    ChatMessageData SendFriendMessage(string ownerPlayerId, string friendPlayerId, string messageText);
    int LoadUnreadMessageCount(string ownerPlayerId);
    ChatUnreadSummaryData LoadUnreadSummary(string ownerPlayerId);
    void MarkFriendMessagesRead(string ownerPlayerId, string friendPlayerId);
    void ClearFriendMessages(string ownerPlayerId, string friendPlayerId);
}
