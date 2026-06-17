using WorkDemoServer.Contracts;
using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public sealed class ChatService
{
    private const int MaxMessageLength = 100;
    private const int MaxConversationMessages = 50;

    private readonly AccountService accounts;
    private readonly ISocialRepository socialRepository;

    public ChatService(
        AccountService accounts,
        ISocialRepository socialRepository)
    {
        this.accounts = accounts;
        this.socialRepository = socialRepository;
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetFriendMessagesAsync(
        string ownerPlayerId,
        string friendPlayerId,
        bool markRead,
        CancellationToken cancellationToken)
    {
        if (!await socialRepository.AreFriendsAsync(ownerPlayerId, friendPlayerId, cancellationToken))
        {
            return Array.Empty<ChatMessageResponse>();
        }

        if (markRead)
        {
            await socialRepository.MarkFriendMessagesReadAsync(
                ownerPlayerId,
                friendPlayerId,
                cancellationToken);
        }

        var messages = await socialRepository.GetFriendMessagesAsync(
            ownerPlayerId,
            friendPlayerId,
            MaxConversationMessages,
            cancellationToken);

        return messages.Select(ToResponse).ToList();
    }

    public async Task<(ChatResult Result, ChatMessageResponse? Message)> SendFriendMessageAsync(
        string ownerPlayerId,
        string friendPlayerId,
        string messageText,
        CancellationToken cancellationToken)
    {
        friendPlayerId = friendPlayerId.Trim();
        var safeText = SanitizeMessage(messageText);

        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return (ChatResult.Fail(ChatErrorCode.Validation, "Friend player ID is required."), null);
        }

        if (string.Equals(ownerPlayerId, friendPlayerId, StringComparison.Ordinal))
        {
            return (ChatResult.Fail(ChatErrorCode.Validation, "You cannot chat with yourself."), null);
        }

        if (string.IsNullOrWhiteSpace(safeText))
        {
            return (ChatResult.Fail(ChatErrorCode.Validation, "Message text is required."), null);
        }

        var friendProfile = await accounts.GetProfileAsync(friendPlayerId, cancellationToken);
        if (friendProfile == null)
        {
            return (ChatResult.Fail(ChatErrorCode.AccountNotFound, "Friend account does not exist."), null);
        }

        if (!await socialRepository.AreFriendsAsync(ownerPlayerId, friendPlayerId, cancellationToken))
        {
            return (ChatResult.Fail(ChatErrorCode.NotFriends, "Players are not friends."), null);
        }

        var message = new ChatMessageRecord
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SenderPlayerId = ownerPlayerId,
            ReceiverPlayerId = friendPlayerId,
            MessageText = safeText,
            SentAt = DateTimeOffset.UtcNow
        };

        await socialRepository.AddChatMessageAsync(message, cancellationToken);
        return (ChatResult.Success(), ToResponse(message));
    }

    public async Task<ChatResult> MarkFriendMessagesReadAsync(
        string ownerPlayerId,
        string friendPlayerId,
        CancellationToken cancellationToken)
    {
        friendPlayerId = friendPlayerId.Trim();
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return ChatResult.Fail(ChatErrorCode.Validation, "Friend player ID is required.");
        }

        if (!await socialRepository.AreFriendsAsync(ownerPlayerId, friendPlayerId, cancellationToken))
        {
            return ChatResult.Fail(ChatErrorCode.NotFriends, "Players are not friends.");
        }

        await socialRepository.MarkFriendMessagesReadAsync(
            ownerPlayerId,
            friendPlayerId,
            cancellationToken);
        return ChatResult.Success();
    }

    public async Task<ChatUnreadSummaryResponse> GetUnreadSummaryAsync(
        string ownerPlayerId,
        CancellationToken cancellationToken)
    {
        var counts = await socialRepository.GetUnreadFriendMessageCountsAsync(
            ownerPlayerId,
            cancellationToken);
        var friends = counts
            .OrderByDescending(item => item.Value)
            .Select(item => new FriendUnreadResponse(item.Key, item.Value))
            .ToList();

        return new ChatUnreadSummaryResponse(friends.Sum(item => item.UnreadCount), friends);
    }

    private static string SanitizeMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return string.Empty;
        }

        var trimmed = messageText.Trim();
        return trimmed.Length > MaxMessageLength ? trimmed[..MaxMessageLength] : trimmed;
    }

    private static ChatMessageResponse ToResponse(ChatMessageRecord message)
    {
        return new ChatMessageResponse(
            message.MessageId,
            message.SenderPlayerId,
            message.ReceiverPlayerId,
            message.MessageText,
            message.SentAt);
    }
}
