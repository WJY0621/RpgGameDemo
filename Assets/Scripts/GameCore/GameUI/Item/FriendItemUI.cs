using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendItemUI : MonoBehaviour
{
    private const float ChatDotSize = 18f;

    private FriendPanel owner;
    private FriendInfo friendInfo;
    private Image friendIcon;
    private TMP_Text tmpFriendName;
    private TMP_Text tmpOnlineText;
    private Text legacyFriendName;
    private Text legacyOnlineText;
    private Button chatButton;
    private Button onlineButton;
    private Button deleteButton;
    private GameObject deleteBK;
    private GameObject chatRedDotRoot;
    private TMP_Text chatRedDotText;
    private bool initialized;
    private bool subscribed;

    public FriendInfo Friend => friendInfo;

    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Transform iconTransform = transform.Find("FriendIcon");
        Transform nameTransform = transform.Find("FriendName");
        Transform onlineTextTransform = transform.Find("IsOnlineText");

        friendIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        tmpFriendName = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
        tmpOnlineText = onlineTextTransform != null ? onlineTextTransform.GetComponent<TMP_Text>() : null;
        legacyFriendName = nameTransform != null ? nameTransform.GetComponent<Text>() : null;
        legacyOnlineText = onlineTextTransform != null ? onlineTextTransform.GetComponent<Text>() : null;
        chatButton = GetButton(transform.Find("ChatButton"));
        onlineButton = GetButton(transform.Find("OnlineButton"));
        deleteButton = GetButton(transform.Find("DeleteButton"));

        Transform deleteBKTransform = transform.Find("DeleteBK");
        deleteBK = deleteBKTransform != null ? deleteBKTransform.gameObject : null;

        BindButton(chatButton, OnClickChat);
        BindButton(onlineButton, OnClickOnline);
        BindButton(deleteButton, OnClickDelete);
        EnsureChatRedDot();
        SubscribeEvents();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshChatRedDot();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void Refresh(FriendPanel panel, FriendInfo info, bool deleteMode, GameObject sharedDeleteBK)
    {
        Init();
        owner = panel;
        friendInfo = info != null ? info.Clone() : null;

        string displayName = friendInfo != null && !string.IsNullOrWhiteSpace(friendInfo.displayName)
            ? friendInfo.displayName
            : friendInfo?.playerId ?? string.Empty;

        SetText(tmpFriendName, legacyFriendName, displayName);
        RefreshOnlineState();
        RefreshChatRedDot();
        SetDeleteMode(deleteMode, sharedDeleteBK);

        if (friendIcon != null)
        {
            SocialAvatarIcon.ApplyDefault(friendIcon);
        }
    }

    public void SetDeleteMode(bool deleteMode, GameObject sharedDeleteBK)
    {
        SetObjectActive(chatButton != null ? chatButton.gameObject : null, !deleteMode);
        SetObjectActive(onlineButton != null ? onlineButton.gameObject : null, !deleteMode);
        SetObjectActive(deleteButton != null ? deleteButton.gameObject : null, deleteMode);
        RefreshChatRedDot();

        if (deleteBK != null)
        {
            deleteBK.SetActive(deleteMode);
        }
        else if (sharedDeleteBK != null)
        {
            sharedDeleteBK.SetActive(deleteMode);
        }
    }

    private void RefreshOnlineState()
    {
        bool isOnline = friendInfo != null && friendInfo.onlineState != FriendOnlineState.Offline;
        string text = isOnline ? "在线" : "离线";
        Color color = isOnline ? new Color(0.25f, 0.9f, 0.35f) : new Color(1f, 0.25f, 0.25f);

        SetText(tmpOnlineText, legacyOnlineText, text);
        SetTextColor(tmpOnlineText, legacyOnlineText, color);
    }

    private void OnClickChat()
    {
        if (friendInfo == null)
        {
            return;
        }

        OpenChatAsync().Forget();
    }

    private void SubscribeEvents()
    {
        if (subscribed || GameMgr.Chat == null)
        {
            return;
        }

        GameMgr.Chat.OnUnreadMessageCountChanged += HandleUnreadChanged;
        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!subscribed || GameMgr.Chat == null)
        {
            subscribed = false;
            return;
        }

        GameMgr.Chat.OnUnreadMessageCountChanged -= HandleUnreadChanged;
        subscribed = false;
    }

    private void HandleUnreadChanged(int _)
    {
        RefreshChatRedDot();
    }

    private void EnsureChatRedDot()
    {
        if (chatButton == null || chatRedDotRoot != null)
        {
            return;
        }

        Transform existing = chatButton.transform.Find("RedDot");
        if (existing != null)
        {
            chatRedDotRoot = existing.gameObject;
            chatRedDotText = chatRedDotRoot.GetComponentInChildren<TMP_Text>(true);
            return;
        }

        chatRedDotRoot = new GameObject("RedDot", typeof(RectTransform), typeof(Image));
        chatRedDotRoot.transform.SetParent(chatButton.transform, false);

        RectTransform rect = chatRedDotRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-4f, -4f);
        rect.sizeDelta = new Vector2(ChatDotSize, ChatDotSize);

        Image image = chatRedDotRoot.GetComponent<Image>();
        image.color = new Color(0.92f, 0.08f, 0.08f, 1f);

        GameObject textObject = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(chatRedDotRoot.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        chatRedDotText = textObject.GetComponent<TMP_Text>();
        chatRedDotText.alignment = TextAlignmentOptions.Center;
        chatRedDotText.color = Color.white;
        chatRedDotText.fontSize = 12f;
        chatRedDotText.raycastTarget = false;
    }

    private void RefreshChatRedDot()
    {
        EnsureChatRedDot();
        if (chatRedDotRoot == null)
        {
            return;
        }

        int unreadCount = friendInfo != null && GameMgr.Chat != null
            ? GameMgr.Chat.GetFriendUnreadCount(friendInfo.playerId)
            : 0;
        bool visible = unreadCount > 0 &&
                       chatButton != null &&
                       chatButton.gameObject.activeInHierarchy;

        chatRedDotRoot.SetActive(visible);
        if (chatRedDotText != null)
        {
            chatRedDotText.gameObject.SetActive(visible);
            chatRedDotText.text = unreadCount > 99 ? "99+" : unreadCount.ToString();
        }
    }

    private async UniTaskVoid OpenChatAsync()
    {
        ChatPanel panel = await GameMgr.UI.ShowPanel<ChatPanel>();
        if (panel != null)
        {
            panel.OpenForFriend(friendInfo);
        }
    }

    private void OnClickOnline()
    {
        if (friendInfo == null)
        {
            return;
        }

        OpenOnlinePanelAsync().Forget();
    }

    private async UniTaskVoid OpenOnlinePanelAsync()
    {
        OnlinePanel panel = await GameMgr.UI.ShowPanel<OnlinePanel>();
        if (panel != null)
        {
            panel.OpenForFriend(friendInfo);
        }
    }

    private void OnClickDelete()
    {
        if (friendInfo == null)
        {
            return;
        }

        owner?.RequestDeleteFriend(friendInfo);
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetText(TMP_Text tmpText, Text legacyText, string content)
    {
        if (tmpText != null)
        {
            tmpText.text = content ?? string.Empty;
        }

        if (legacyText != null)
        {
            legacyText.text = content ?? string.Empty;
        }
    }

    private static void SetTextColor(TMP_Text tmpText, Text legacyText, Color color)
    {
        if (tmpText != null)
        {
            tmpText.color = color;
        }

        if (legacyText != null)
        {
            legacyText.color = color;
        }
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
