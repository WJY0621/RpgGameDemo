using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FriendPanel : BasePanel
{
    private RectTransform friendContent;
    private FriendItemUI friendItemTemplate;
    private Image playerIcon;
    private TMP_Text tmpIdText;
    private Text legacyIdText;
    private TMP_Text tmpPlayerNameText;
    private Text legacyPlayerNameText;
    private Button addFriendButton;
    private Button applyFriendButton;
    private Button deleteFriendButton;
    private Button closeButton;
    private GameObject sharedDeleteBK;
    private readonly List<FriendItemUI> spawnedItems = new List<FriendItemUI>();
    private bool initialized;
    private bool deleteMode;
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
        RefreshPlayerInfo();
        GameMgr.Social?.RefreshFriends();
        RefreshFriends();
    }

    public override void Hide(UnityAction callBack = null)
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

    public void RequestDeleteFriend(FriendInfo friend)
    {
        if (friend == null || string.IsNullOrWhiteSpace(friend.playerId))
        {
            return;
        }

        ConfirmDeleteFriendAsync(friend).Forget();
    }

    private void BindReferences()
    {
        Transform contentTransform = transform.Find("FriendContent");
        friendContent = contentTransform as RectTransform;

        Transform itemTransform = contentTransform != null ? contentTransform.Find("FriendItem") : null;
        if (itemTransform != null)
        {
            friendItemTemplate = itemTransform.GetComponent<FriendItemUI>();
            if (friendItemTemplate == null)
            {
                friendItemTemplate = itemTransform.gameObject.AddComponent<FriendItemUI>();
            }

            friendItemTemplate.Init();
            friendItemTemplate.gameObject.SetActive(false);
        }

        addFriendButton = GetButton(transform.Find("AddFriendButton"));
        applyFriendButton = GetButton(transform.Find("ApplyFriendButton"));
        deleteFriendButton = GetButton(transform.Find("DeleteFriendButton"));
        closeButton = GetButton(transform.Find("CloseButton"));
        Transform idTextTransform = transform.Find("IDText");
        tmpIdText = idTextTransform != null ? idTextTransform.GetComponent<TMP_Text>() : null;
        legacyIdText = idTextTransform != null ? idTextTransform.GetComponent<Text>() : null;
        Transform playerIconTransform = transform.Find("PlayerIcon");
        playerIcon = playerIconTransform != null ? playerIconTransform.GetComponent<Image>() : null;
        Transform playerNameTransform = transform.Find("PlayerNameText");
        tmpPlayerNameText = playerNameTransform != null ? playerNameTransform.GetComponent<TMP_Text>() : null;
        legacyPlayerNameText = playerNameTransform != null ? playerNameTransform.GetComponent<Text>() : null;

        Transform deleteBKTransform = transform.Find("DeleteBK");
        sharedDeleteBK = deleteBKTransform != null ? deleteBKTransform.gameObject : null;
        if (sharedDeleteBK != null)
        {
            sharedDeleteBK.SetActive(false);
        }
    }

    private void BindEvents()
    {
        BindButton(addFriendButton, OnClickAddFriend);
        BindButton(applyFriendButton, OnClickApplyFriend);
        BindButton(deleteFriendButton, OnClickToggleDeleteMode);
        BindButton(closeButton, OnClickClose);
        ConfigureRedDot(applyFriendButton != null ? applyFriendButton.transform : null, RedDotType.FriendRequest, true);
    }

    private void SubscribeEvents()
    {
        if (subscribed || GameMgr.Social == null)
        {
            return;
        }

        GameMgr.Social.OnFriendsChanged += HandleFriendsChanged;
        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!subscribed || GameMgr.Social == null)
        {
            subscribed = false;
            return;
        }

        GameMgr.Social.OnFriendsChanged -= HandleFriendsChanged;
        subscribed = false;
    }

    private void RefreshFriends()
    {
        RefreshPlayerInfo();
        ClearSpawnedItems();

        if (friendItemTemplate == null || friendContent == null || GameMgr.Social == null)
        {
            RefreshDeleteModeVisual();
            return;
        }

        IReadOnlyList<FriendInfo> friends = GameMgr.Social.Friends;
        for (int i = 0; i < friends.Count; i++)
        {
            FriendInfo friend = friends[i];
            if (friend == null)
            {
                continue;
            }

            GameObject itemObj = Instantiate(friendItemTemplate.gameObject, friendContent);
            itemObj.SetActive(true);

            FriendItemUI itemUI = itemObj.GetComponent<FriendItemUI>();
            if (itemUI == null)
            {
                itemUI = itemObj.AddComponent<FriendItemUI>();
            }

            itemUI.Refresh(this, friend, deleteMode, sharedDeleteBK);
            spawnedItems.Add(itemUI);
        }

        RefreshDeleteModeVisual();
    }

    private void RefreshPlayerInfo()
    {
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        string playerId = profile != null && !string.IsNullOrWhiteSpace(profile.playerId)
            ? profile.playerId
            : "------";
        string playerName = ResolvePlayerName(profile);

        SetText(tmpIdText, legacyIdText, $"ID：{playerId}");
        SetText(tmpPlayerNameText, legacyPlayerNameText, playerName);

        if (playerIcon != null)
        {
            SocialAvatarIcon.ApplyDefault(playerIcon);
        }
    }

    private string ResolvePlayerName(AccountProfile profile)
    {
        if (profile == null)
        {
            return "未登录";
        }

        if (!string.IsNullOrWhiteSpace(profile.displayName))
        {
            return profile.displayName;
        }

        if (!string.IsNullOrWhiteSpace(profile.accountName))
        {
            return profile.accountName;
        }

        return string.IsNullOrWhiteSpace(profile.playerId) ? "未命名" : profile.playerId;
    }

    private void ClearSpawnedItems()
    {
        for (int i = friendContent != null ? friendContent.childCount - 1 : -1; i >= 0; i--)
        {
            Transform child = friendContent.GetChild(i);
            if (friendItemTemplate != null && child == friendItemTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }

        spawnedItems.Clear();
    }

    private void OnClickAddFriend()
    {
        OpenPanelAsync<AddFriendPanel>().Forget();
    }

    private void OnClickApplyFriend()
    {
        OpenPanelAsync<ApplyListPanel>().Forget();
    }

    private void OnClickToggleDeleteMode()
    {
        deleteMode = !deleteMode;
        RefreshDeleteModeVisual();
    }

    private void RefreshDeleteModeVisual()
    {
        if (sharedDeleteBK != null)
        {
            sharedDeleteBK.SetActive(deleteMode);
        }

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                spawnedItems[i].SetDeleteMode(deleteMode, sharedDeleteBK);
            }
        }
    }

    private async UniTaskVoid ConfirmDeleteFriendAsync(FriendInfo friend)
    {
        TipPanel tipPanel = await GameMgr.UI.ShowPanel<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip(
            "是否删除此好友",
            () =>
            {
                if (GameMgr.Social != null && GameMgr.Social.RemoveFriend(friend.playerId))
                {
                    deleteMode = false;
                    RefreshFriends();
                }
            },
            RefreshDeleteModeVisual);
    }

    private async UniTaskVoid OpenPanelAsync<T>() where T : BasePanel
    {
        await GameMgr.UI.ShowPanel<T>();
    }

    private void HandleFriendsChanged()
    {
        RefreshFriends();
    }

    private void StartRefreshLoop()
    {
        StopRefreshLoop();
        refreshLoopCts = new CancellationTokenSource();
        RefreshFriendsLoopAsync(refreshLoopCts.Token).Forget();
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

    private async UniTaskVoid RefreshFriendsLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool canceled = await UniTask.Delay(1500, cancellationToken: cancellationToken).SuppressCancellationThrow();
            if (canceled || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!isActiveAndEnabled || !IsShow)
            {
                continue;
            }

            GameMgr.Social?.RefreshFriends(false);
        }
    }

    private void OnClickClose()
    {
        deleteMode = false;
        RefreshDeleteModeVisual();
        GameMgr.UI?.HidePanel<FriendPanel>();
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

    private static void ConfigureRedDot(Transform targetTransform, RedDotType type, bool showNumber)
    {
        if (targetTransform == null)
        {
            return;
        }

        RedDotView redDotView = targetTransform.GetComponent<RedDotView>();
        if (redDotView == null)
        {
            redDotView = targetTransform.gameObject.AddComponent<RedDotView>();
        }

        redDotView.Configure(type, showNumber);
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
}
