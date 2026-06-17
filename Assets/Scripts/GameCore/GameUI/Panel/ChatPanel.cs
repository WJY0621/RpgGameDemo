using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanel : BasePanel
{
    private const int RefreshIntervalMilliseconds = 1500;

    private TMP_Text friendNameText;
    private TMP_InputField tmpChatField;
    private InputField legacyChatField;
    private Image friendIcon;
    private Image playerIcon;
    private Button sendButton;
    private Button closeButton;
    private ScrollRect scrollRect;
    private RectTransform contentRoot;
    private ChatMessageItem messageTemplate;
    private UIPanelDragHandle dragHandle;
    private string friendPlayerId;
    private string friendDisplayName;
    private bool initialized;
    private bool subscribed;
    private CancellationTokenSource refreshLoopCts;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        BindReferences();
        BindEvents();
        SubscribeEvents();
    }

    public override void Show()
    {
        base.Show();
        Init();
        StartRefreshLoop();
        RefreshAll();
    }

    public override void Hide(UnityEngine.Events.UnityAction callBack = null)
    {
        StopRefreshLoop();
        base.Hide(callBack);
    }

    private void OnEnable()
    {
        SubscribeEvents();
        StartRefreshLoop();
    }

    private void OnDisable()
    {
        StopRefreshLoop();
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        StopRefreshLoop();
        UnsubscribeEvents();
    }

    public void OpenForFriend(FriendInfo friend)
    {
        if (friend == null)
        {
            return;
        }

        friendPlayerId = friend.playerId;
        friendDisplayName = string.IsNullOrWhiteSpace(friend.displayName) ? friend.playerId : friend.displayName;
        RefreshAll();
    }

    private void StartRefreshLoop()
    {
        StopRefreshLoop();
        refreshLoopCts = new CancellationTokenSource();
        RefreshMessagesLoopAsync(refreshLoopCts.Token).Forget();
    }

    private void StopRefreshLoop()
    {
        if (refreshLoopCts == null)
        {
            return;
        }

        refreshLoopCts.Cancel();
        refreshLoopCts.Dispose();
        refreshLoopCts = null;
    }

    private async UniTaskVoid RefreshMessagesLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool canceled = await UniTask.Delay(
                RefreshIntervalMilliseconds,
                cancellationToken: cancellationToken).SuppressCancellationThrow();
            if (canceled || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!isActiveAndEnabled || !IsShow || string.IsNullOrWhiteSpace(friendPlayerId))
            {
                continue;
            }

            RefreshMessages();
        }
    }

    public void OpenForFriend(string targetFriendPlayerId)
    {
        FriendInfo friend = GameMgr.Social != null ? GameMgr.Social.GetFriend(targetFriendPlayerId) : null;
        if (friend != null)
        {
            OpenForFriend(friend);
            return;
        }

        friendPlayerId = targetFriendPlayerId;
        friendDisplayName = targetFriendPlayerId;
        RefreshAll();
    }

    private void BindReferences()
    {
        friendNameText = GetText(transform.Find("FriendNameText"));
        if (friendNameText == null)
        {
            friendNameText = GetText(transform.Find("FrienNameText"));
        }

        friendIcon = GetImage(transform.Find("FriendIcon"));
        playerIcon = GetImage(transform.Find("PlayerIcon"));

        Transform inputTransform = transform.Find("PlayerChatField");
        tmpChatField = inputTransform != null ? inputTransform.GetComponent<TMP_InputField>() : null;
        legacyChatField = inputTransform != null ? inputTransform.GetComponent<InputField>() : null;

        sendButton = GetButton(transform.Find("SendButton"));
        closeButton = GetButton(transform.Find("CloseButton"));

        Transform scrollTransform = transform.Find("ChatContent/Scroll View");
        scrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        Transform contentTransform = transform.Find("ChatContent/Scroll View/Viewport/Content");
        contentRoot = contentTransform as RectTransform;

        Transform templateTransform = contentTransform != null ? contentTransform.Find("ChatMessageItem") : null;
        if (templateTransform != null)
        {
            messageTemplate = templateTransform.GetComponent<ChatMessageItem>();
            if (messageTemplate == null)
            {
                messageTemplate = templateTransform.gameObject.AddComponent<ChatMessageItem>();
            }

            messageTemplate.Init();
            messageTemplate.gameObject.SetActive(false);
        }

        Transform dragTransform = transform.Find("DragContent");
        if (dragTransform != null)
        {
            dragHandle = dragTransform.GetComponent<UIPanelDragHandle>();
            if (dragHandle == null)
            {
                dragHandle = dragTransform.gameObject.AddComponent<UIPanelDragHandle>();
            }

            dragHandle.Setup(transform as RectTransform);
        }
    }

    private void BindEvents()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendCurrentMessage);
            sendButton.onClick.AddListener(SendCurrentMessage);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (tmpChatField != null)
        {
            tmpChatField.onSubmit.RemoveListener(OnSubmitMessage);
            tmpChatField.onSubmit.AddListener(OnSubmitMessage);
        }

        if (legacyChatField != null)
        {
            legacyChatField.onEndEdit.RemoveListener(OnSubmitMessage);
            legacyChatField.onEndEdit.AddListener(OnSubmitMessage);
        }
    }

    private void SubscribeEvents()
    {
        if (subscribed || GameMgr.Chat == null)
        {
            return;
        }

        GameMgr.Chat.OnFriendConversationChanged += HandleConversationChanged;
        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!subscribed || GameMgr.Chat == null)
        {
            subscribed = false;
            return;
        }

        GameMgr.Chat.OnFriendConversationChanged -= HandleConversationChanged;
        subscribed = false;
    }

    private void RefreshAll()
    {
        RefreshTitle();
        RefreshIcons();
        RefreshMessages();
    }

    private void RefreshTitle()
    {
        if (friendNameText != null)
        {
            friendNameText.text = string.IsNullOrWhiteSpace(friendDisplayName) ? "Friend" : friendDisplayName;
        }
    }

    private void RefreshIcons()
    {
        SocialAvatarIcon.ApplyDefault(friendIcon);
        SocialAvatarIcon.ApplyDefault(playerIcon);
    }

    private void RefreshMessages()
    {
        ClearSpawnedMessages();

        if (messageTemplate == null || contentRoot == null || GameMgr.Chat == null)
        {
            return;
        }

        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile == null || !profile.IsValid() || string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return;
        }

        List<ChatMessageData> messages = GameMgr.Chat.LoadFriendMessages(friendPlayerId);
        for (int i = 0; i < messages.Count; i++)
        {
            ChatMessageData message = messages[i];
            if (message == null)
            {
                continue;
            }

            GameObject itemObj = Instantiate(messageTemplate.gameObject, contentRoot);
            itemObj.SetActive(true);

            ChatMessageItem item = itemObj.GetComponent<ChatMessageItem>();
            if (item == null)
            {
                item = itemObj.AddComponent<ChatMessageItem>();
            }

            item.SetMessage(message, profile.playerId, friendDisplayName, profile.displayName);
        }

        ScrollToBottomNextFrame().Forget();
    }

    private void ClearSpawnedMessages()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (messageTemplate != null && child == messageTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void SendCurrentMessage()
    {
        if (GameMgr.Chat == null || string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return;
        }

        string content = GetInputText();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ChatMessageData sentMessage = GameMgr.Chat.SendFriendMessage(friendPlayerId, content);
        if (sentMessage == null)
        {
            return;
        }

        SetInputText(string.Empty);
        RefreshMessages();
        FocusInputField();
    }

    private void OnSubmitMessage(string value)
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return;
        }

        SendCurrentMessage();
    }

    private void HandleConversationChanged(string changedFriendPlayerId)
    {
        if (!string.Equals(changedFriendPlayerId, friendPlayerId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshMessages();
    }

    private async UniTaskVoid ScrollToBottomNextFrame()
    {
        await UniTask.Yield();
        Canvas.ForceUpdateCanvases();

        if (contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private string GetInputText()
    {
        if (tmpChatField != null)
        {
            return tmpChatField.text;
        }

        return legacyChatField != null ? legacyChatField.text : string.Empty;
    }

    private void SetInputText(string value)
    {
        if (tmpChatField != null)
        {
            tmpChatField.SetTextWithoutNotify(value);
        }

        if (legacyChatField != null)
        {
            legacyChatField.SetTextWithoutNotify(value);
        }
    }

    private void FocusInputField()
    {
        if (tmpChatField != null)
        {
            tmpChatField.ActivateInputField();
            return;
        }

        legacyChatField?.ActivateInputField();
    }

    private void Close()
    {
        GameMgr.UI?.HidePanel<ChatPanel>();
    }

    private static TMP_Text GetText(Transform target)
    {
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Image GetImage(Transform target)
    {
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }
}
