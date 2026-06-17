using System;

[Serializable]
public class ChatMessageData
{
    public string messageId;
    public ChatChannelType channelType;
    public string senderPlayerId;
    public string receiverPlayerId;
    public string messageText;
    public long sentAtUnixSeconds;

    public ChatMessageData Clone()
    {
        return new ChatMessageData
        {
            messageId = messageId,
            channelType = channelType,
            senderPlayerId = senderPlayerId,
            receiverPlayerId = receiverPlayerId,
            messageText = messageText,
            sentAtUnixSeconds = sentAtUnixSeconds
        };
    }
}
