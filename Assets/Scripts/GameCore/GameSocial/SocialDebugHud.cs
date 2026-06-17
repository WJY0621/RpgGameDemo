using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SocialDebugHud : MonoBehaviour
{
    private bool visible;
    private string displayName = "Player";
    private string searchPlayerId = string.Empty;
    private string selectedFriendId = string.Empty;
    private string chatText = string.Empty;
    private Vector2 friendScroll;
    private Vector2 chatScroll;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(292f, 16f, 380f, 560f), "Social Debug (F7)", GUI.skin.window);
        DrawAccountSection();
        GUILayout.Space(8f);
        DrawSearchSection();
        GUILayout.Space(8f);
        DrawFriendSection();
        GUILayout.Space(8f);
        DrawChatSection();
        GUILayout.EndArea();
    }

    private void DrawAccountSection()
    {
        GUILayout.Label("Account");
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile != null && profile.IsValid())
        {
            GUILayout.Label($"ID: {profile.playerId}");
            GUILayout.Label($"Name: {profile.displayName}");
        }
        else
        {
            GUILayout.Label("Not logged in");
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Name", GUILayout.Width(52f));
        displayName = GUILayout.TextField(displayName);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Login/Create"))
        {
            GameMgr.Account?.LoginOrCreate(displayName);
            GameMgr.Social?.RefreshFriends();
        }

        if (GUILayout.Button("New Account"))
        {
            GameMgr.Account?.CreateNewAccount(displayName);
            GameMgr.Social?.RefreshFriends();
        }

        if (GUILayout.Button("Clear"))
        {
            GameMgr.Social?.ClearFriends();
            GameMgr.Account?.ClearLocalAccount();
            selectedFriendId = string.Empty;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSearchSection()
    {
        GUILayout.Label("Search Player ID");
        GUILayout.BeginHorizontal();
        searchPlayerId = GUILayout.TextField(searchPlayerId);
        if (GUILayout.Button("Search", GUILayout.Width(72f)))
        {
            GameMgr.Social?.SearchPlayerById(searchPlayerId);
        }
        GUILayout.EndHorizontal();

        FriendInfo result = GameMgr.Social != null ? GameMgr.Social.LastSearchResult : null;
        if (result == null)
        {
            GUILayout.Label("No search result");
            return;
        }

        GUILayout.Label($"{result.displayName} ({result.playerId}) - {result.onlineState}");
        if (GUILayout.Button("Add Friend"))
        {
            GameMgr.Social.AddLastSearchResultAsFriend();
        }
    }

    private void DrawFriendSection()
    {
        GUILayout.Label("Friends");
        if (GUILayout.Button("Open FriendPanel"))
        {
            OpenFriendPanelAsync().Forget();
        }

        IReadOnlyList<FriendInfo> friends = GameMgr.Social != null
            ? GameMgr.Social.Friends
            : new List<FriendInfo>();

        friendScroll = GUILayout.BeginScrollView(friendScroll, GUILayout.Height(150f));
        for (int i = 0; i < friends.Count; i++)
        {
            FriendInfo friend = friends[i];
            if (friend == null)
            {
                continue;
            }

            GUILayout.BeginHorizontal();
            string label = $"{friend.displayName} ({friend.playerId}) [{friend.onlineState}]";
            if (GUILayout.Button(label))
            {
                selectedFriendId = friend.playerId;
            }

            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                GameMgr.Social.RemoveFriend(friend.playerId);
                if (selectedFriendId == friend.playerId)
                {
                    selectedFriendId = string.Empty;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawChatSection()
    {
        GUILayout.Label("Friend Chat");
        if (string.IsNullOrWhiteSpace(selectedFriendId))
        {
            GUILayout.Label("Select a friend first");
            return;
        }

        GUILayout.Label($"Selected: {selectedFriendId}");
        if (GUILayout.Button("Open ChatPanel"))
        {
            OpenChatPanelAsync().Forget();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Mock Join Request"))
        {
            OpenMockOnlineRequestAsync(InviteType.RequestToJoinWorld).Forget();
        }

        if (GUILayout.Button("Mock Invite"))
        {
            OpenMockOnlineRequestAsync(InviteType.InviteToMyWorld).Forget();
        }
        GUILayout.EndHorizontal();

        List<ChatMessageData> messages = GameMgr.Chat != null
            ? GameMgr.Chat.LoadFriendMessages(selectedFriendId)
            : new List<ChatMessageData>();

        chatScroll = GUILayout.BeginScrollView(chatScroll, GUILayout.Height(120f));
        for (int i = 0; i < messages.Count; i++)
        {
            ChatMessageData message = messages[i];
            if (message == null)
            {
                continue;
            }

            GUILayout.Label($"{message.senderPlayerId}: {message.messageText}");
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        chatText = GUILayout.TextField(chatText);
        if (GUILayout.Button("Send", GUILayout.Width(64f)))
        {
            GameMgr.Chat?.SendFriendMessage(selectedFriendId, chatText);
            chatText = string.Empty;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Clear Chat"))
        {
            GameMgr.Chat?.ClearFriendMessages(selectedFriendId);
        }
    }

    private async UniTaskVoid OpenChatPanelAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedFriendId) || GameMgr.Social == null)
        {
            return;
        }

        ChatPanel panel = await GameMgr.UI.ShowPanel<ChatPanel>();
        FriendInfo friend = GameMgr.Social != null ? GameMgr.Social.GetFriend(selectedFriendId) : null;
        if (panel != null && friend != null)
        {
            panel.OpenForFriend(friend);
        }
        else if (panel != null)
        {
            panel.OpenForFriend(selectedFriendId);
        }
    }

    private async UniTaskVoid OpenFriendPanelAsync()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        await GameMgr.UI.ShowPanel<FriendPanel>();
    }

    private async UniTaskVoid OpenMockOnlineRequestAsync(InviteType inviteType)
    {
        if (string.IsNullOrWhiteSpace(selectedFriendId) || GameMgr.UI == null)
        {
            return;
        }

        FriendInfo friend = GameMgr.Social != null ? GameMgr.Social.GetFriend(selectedFriendId) : null;
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        string friendPlayerId = friend != null ? friend.playerId : selectedFriendId;

        InviteData inviteData = new InviteData
        {
            inviteId = System.Guid.NewGuid().ToString("N"),
            inviteType = inviteType,
            requesterPlayerId = friendPlayerId,
            requesterDisplayName = friend != null ? friend.displayName : friendPlayerId,
            hostPlayerId = inviteType == InviteType.RequestToJoinWorld
                ? profile != null ? profile.playerId : string.Empty
                : friendPlayerId,
            targetPlayerId = profile != null ? profile.playerId : string.Empty,
            address = "127.0.0.1",
            port = 7777,
            createdAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        GameMgr.Social.ReceiveOnlineRequest(inviteData);
        await UniTask.Yield();
    }
}
