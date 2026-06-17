using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public interface ISocialRepository
{
    Task<bool> AreFriendsAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FriendshipRecord>> GetFriendshipsAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task AddFriendshipAsync(
        FriendshipRecord friendship,
        CancellationToken cancellationToken);

    Task RemoveFriendshipAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken);

    Task<FriendRequestRecord?> FindFriendRequestAsync(
        string requesterPlayerId,
        string targetPlayerId,
        CancellationToken cancellationToken);

    Task<FriendRequestRecord?> FindFriendRequestByIdAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FriendRequestRecord>> GetIncomingFriendRequestsAsync(
        string targetPlayerId,
        CancellationToken cancellationToken);

    Task AddFriendRequestAsync(
        FriendRequestRecord request,
        CancellationToken cancellationToken);

    Task RemoveFriendRequestAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task RemoveFriendRequestsBetweenAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken);

    Task AddChatMessageAsync(
        ChatMessageRecord message,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessageRecord>> GetFriendMessagesAsync(
        string ownerPlayerId,
        string friendPlayerId,
        int maxMessages,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> GetUnreadFriendMessageCountsAsync(
        string ownerPlayerId,
        CancellationToken cancellationToken);

    Task MarkFriendMessagesReadAsync(
        string ownerPlayerId,
        string friendPlayerId,
        CancellationToken cancellationToken);
}
