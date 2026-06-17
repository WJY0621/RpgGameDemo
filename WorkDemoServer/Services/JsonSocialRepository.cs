using System.Text.Json;
using Microsoft.Extensions.Options;
using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public sealed class JsonSocialRepository : ISocialRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonSocialRepository(IOptions<SocialStoreOptions> options, IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.FilePath;
        storePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public async Task<bool> AreFriendsAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.Friendships.Any(friendship =>
                IsSameFriendship(friendship, firstPlayerId, secondPlayerId));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<FriendshipRecord>> GetFriendshipsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.Friendships
                .Where(friendship =>
                    string.Equals(friendship.PlayerAId, playerId, StringComparison.Ordinal) ||
                    string.Equals(friendship.PlayerBId, playerId, StringComparison.Ordinal))
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AddFriendshipAsync(
        FriendshipRecord friendship,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            if (!store.Friendships.Any(item =>
                IsSameFriendship(item, friendship.PlayerAId, friendship.PlayerBId)))
            {
                store.Friendships.Add(friendship);
                await SaveStoreAsync(store, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveFriendshipAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.Friendships.RemoveAll(friendship =>
                IsSameFriendship(friendship, firstPlayerId, secondPlayerId));
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FriendRequestRecord?> FindFriendRequestAsync(
        string requesterPlayerId,
        string targetPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.FriendRequests.FirstOrDefault(request =>
                string.Equals(request.RequesterPlayerId, requesterPlayerId, StringComparison.Ordinal) &&
                string.Equals(request.TargetPlayerId, targetPlayerId, StringComparison.Ordinal));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FriendRequestRecord?> FindFriendRequestByIdAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.FriendRequests.FirstOrDefault(request =>
                string.Equals(request.RequestId, requestId, StringComparison.Ordinal));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<FriendRequestRecord>> GetIncomingFriendRequestsAsync(
        string targetPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.FriendRequests
                .Where(request => string.Equals(request.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
                .OrderByDescending(request => request.CreatedAt)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AddFriendRequestAsync(
        FriendRequestRecord request,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.FriendRequests.Add(request);
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveFriendRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.FriendRequests.RemoveAll(request =>
                string.Equals(request.RequestId, requestId, StringComparison.Ordinal));
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveFriendRequestsBetweenAsync(
        string firstPlayerId,
        string secondPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.FriendRequests.RemoveAll(request =>
                IsSameRequestPair(request, firstPlayerId, secondPlayerId));
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AddChatMessageAsync(
        ChatMessageRecord message,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            store.ChatMessages.Add(message);
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatMessageRecord>> GetFriendMessagesAsync(
        string ownerPlayerId,
        string friendPlayerId,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.ChatMessages
                .Where(message => IsSameConversation(message, ownerPlayerId, friendPlayerId))
                .OrderByDescending(message => message.SentAt)
                .Take(Math.Max(1, maxMessages))
                .OrderBy(message => message.SentAt)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> GetUnreadFriendMessageCountsAsync(
        string ownerPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            return store.ChatMessages
                .Where(message =>
                    string.Equals(message.ReceiverPlayerId, ownerPlayerId, StringComparison.Ordinal) &&
                    message.ReadAt == null)
                .GroupBy(message => message.SenderPlayerId)
                .ToDictionary(group => group.Key, group => group.Count());
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task MarkFriendMessagesReadAsync(
        string ownerPlayerId,
        string friendPlayerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var changed = false;

            for (var i = 0; i < store.ChatMessages.Count; i++)
            {
                var message = store.ChatMessages[i];
                if (message.ReadAt == null &&
                    string.Equals(message.SenderPlayerId, friendPlayerId, StringComparison.Ordinal) &&
                    string.Equals(message.ReceiverPlayerId, ownerPlayerId, StringComparison.Ordinal))
                {
                    message.ReadAt = now;
                    changed = true;
                }
            }

            if (changed)
            {
                await SaveStoreAsync(store, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SocialStoreData> LoadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storePath))
        {
            return new SocialStoreData();
        }

        await using var stream = File.OpenRead(storePath);
        var store = await JsonSerializer.DeserializeAsync<SocialStoreData>(
            stream,
            JsonOptions,
            cancellationToken);

        store ??= new SocialStoreData();
        store.FriendRequests ??= new List<FriendRequestRecord>();
        store.Friendships ??= new List<FriendshipRecord>();
        store.ChatMessages ??= new List<ChatMessageRecord>();
        return store;
    }

    private async Task SaveStoreAsync(
        SocialStoreData store,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = storePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken);
        }

        if (File.Exists(storePath))
        {
            File.Delete(storePath);
        }

        File.Move(tempPath, storePath);
    }

    private static bool IsSameFriendship(
        FriendshipRecord friendship,
        string firstPlayerId,
        string secondPlayerId)
    {
        return string.Equals(friendship.PlayerAId, firstPlayerId, StringComparison.Ordinal) &&
               string.Equals(friendship.PlayerBId, secondPlayerId, StringComparison.Ordinal) ||
               string.Equals(friendship.PlayerAId, secondPlayerId, StringComparison.Ordinal) &&
               string.Equals(friendship.PlayerBId, firstPlayerId, StringComparison.Ordinal);
    }

    private static bool IsSameRequestPair(
        FriendRequestRecord request,
        string firstPlayerId,
        string secondPlayerId)
    {
        return string.Equals(request.RequesterPlayerId, firstPlayerId, StringComparison.Ordinal) &&
               string.Equals(request.TargetPlayerId, secondPlayerId, StringComparison.Ordinal) ||
               string.Equals(request.RequesterPlayerId, secondPlayerId, StringComparison.Ordinal) &&
               string.Equals(request.TargetPlayerId, firstPlayerId, StringComparison.Ordinal);
    }

    private static bool IsSameConversation(
        ChatMessageRecord message,
        string firstPlayerId,
        string secondPlayerId)
    {
        return string.Equals(message.SenderPlayerId, firstPlayerId, StringComparison.Ordinal) &&
               string.Equals(message.ReceiverPlayerId, secondPlayerId, StringComparison.Ordinal) ||
               string.Equals(message.SenderPlayerId, secondPlayerId, StringComparison.Ordinal) &&
               string.Equals(message.ReceiverPlayerId, firstPlayerId, StringComparison.Ordinal);
    }
}
