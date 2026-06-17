using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ApplyListPanel : BasePanel
{
    private RectTransform applyContent;
    private ApplyItemUI applyItemTemplate;
    private Button closeButton;
    private readonly List<ApplyItemUI> spawnedItems = new List<ApplyItemUI>();
    private bool initialized;
    private bool subscribed;

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
        GameMgr.Social?.RefreshIncomingFriendRequests();
        RefreshRequests();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void AcceptRequest(FriendRequestData request)
    {
        if (request == null || GameMgr.Social == null)
        {
            return;
        }

        GameMgr.Social.AcceptFriendRequest(request.requestId);
    }

    public void RefuseRequest(FriendRequestData request)
    {
        if (request == null || GameMgr.Social == null)
        {
            return;
        }

        GameMgr.Social.RefuseFriendRequest(request.requestId);
    }

    private void BindReferences()
    {
        Transform contentTransform = transform.Find("ApplyContent");
        applyContent = contentTransform as RectTransform;

        Transform itemTransform = contentTransform != null ? contentTransform.Find("ApplyItem") : null;
        if (itemTransform != null)
        {
            applyItemTemplate = itemTransform.GetComponent<ApplyItemUI>();
            if (applyItemTemplate == null)
            {
                applyItemTemplate = itemTransform.gameObject.AddComponent<ApplyItemUI>();
            }

            applyItemTemplate.Init();
            applyItemTemplate.gameObject.SetActive(false);
        }

        closeButton = GetButton(transform.Find("CloseButton"));
    }

    private void BindEvents()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void SubscribeEvents()
    {
        if (subscribed || GameMgr.Social == null)
        {
            return;
        }

        GameMgr.Social.OnFriendRequestsChanged += RefreshRequests;
        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!subscribed || GameMgr.Social == null)
        {
            subscribed = false;
            return;
        }

        GameMgr.Social.OnFriendRequestsChanged -= RefreshRequests;
        subscribed = false;
    }

    private void RefreshRequests()
    {
        ClearSpawnedItems();

        if (applyItemTemplate == null || applyContent == null || GameMgr.Social == null)
        {
            return;
        }

        IReadOnlyList<FriendRequestData> requests = GameMgr.Social.IncomingFriendRequests;
        for (int i = 0; i < requests.Count; i++)
        {
            FriendRequestData request = requests[i];
            if (request == null)
            {
                continue;
            }

            GameObject itemObj = Instantiate(applyItemTemplate.gameObject, applyContent);
            itemObj.SetActive(true);

            ApplyItemUI itemUI = itemObj.GetComponent<ApplyItemUI>();
            if (itemUI == null)
            {
                itemUI = itemObj.AddComponent<ApplyItemUI>();
            }

            itemUI.Refresh(this, request);
            spawnedItems.Add(itemUI);
        }
    }

    private void ClearSpawnedItems()
    {
        for (int i = applyContent != null ? applyContent.childCount - 1 : -1; i >= 0; i--)
        {
            Transform child = applyContent.GetChild(i);
            if (applyItemTemplate != null && child == applyItemTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }

        spawnedItems.Clear();
    }

    private void Close()
    {
        GameMgr.UI?.HidePanel<ApplyListPanel>();
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }
}
