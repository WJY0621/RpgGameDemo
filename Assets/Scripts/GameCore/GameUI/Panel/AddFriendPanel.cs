using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddFriendPanel : BasePanel
{
    private TMP_InputField tmpFriendIdField;
    private InputField legacyFriendIdField;
    private Button searchButton;
    private Button closeButton;
    private AddFriendItemUI addFriendItem;
    private FriendInfo currentSearchResult;
    private bool initialized;

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
        ClearSearchResult();
    }

    public override void Show()
    {
        base.Show();
        Init();
        ClearSearchResult();
    }

    public void SendFriendRequest(FriendInfo targetFriend)
    {
        if (targetFriend == null || GameMgr.Social == null)
        {
            return;
        }

        bool sent = GameMgr.Social.SendFriendRequest(targetFriend);
        if (sent)
        {
            ClearSearchResult();
            SetInputText(string.Empty);
            GameMgr.UI?.HidePanel<AddFriendPanel>();
        }
    }

    private void BindReferences()
    {
        Transform inputTransform = transform.Find("FriendIDField");
        tmpFriendIdField = inputTransform != null ? inputTransform.GetComponent<TMP_InputField>() : null;
        legacyFriendIdField = inputTransform != null ? inputTransform.GetComponent<InputField>() : null;

        searchButton = GetButton(transform.Find("SearchButton"));
        closeButton = GetButton(transform.Find("CloseButton"));

        Transform addFriendItemTransform = transform.Find("AddContent/AddFriendItem");
        if (addFriendItemTransform != null)
        {
            addFriendItem = addFriendItemTransform.GetComponent<AddFriendItemUI>();
            if (addFriendItem == null)
            {
                addFriendItem = addFriendItemTransform.gameObject.AddComponent<AddFriendItemUI>();
            }

            addFriendItem.Init();
        }
    }

    private void BindEvents()
    {
        if (searchButton != null)
        {
            searchButton.onClick.RemoveListener(OnClickSearch);
            searchButton.onClick.AddListener(OnClickSearch);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (tmpFriendIdField != null)
        {
            tmpFriendIdField.onSubmit.RemoveListener(OnSubmitSearch);
            tmpFriendIdField.onSubmit.AddListener(OnSubmitSearch);
        }

        if (legacyFriendIdField != null)
        {
            legacyFriendIdField.onEndEdit.RemoveListener(OnSubmitSearch);
            legacyFriendIdField.onEndEdit.AddListener(OnSubmitSearch);
        }
    }

    private void OnClickSearch()
    {
        SearchByInput();
    }

    private void OnSubmitSearch(string value)
    {
        SearchByInput();
    }

    private void SearchByInput()
    {
        if (GameMgr.Social == null)
        {
            ClearSearchResult();
            return;
        }

        string targetPlayerId = GetInputText();
        if (string.IsNullOrWhiteSpace(targetPlayerId))
        {
            ClearSearchResult();
            return;
        }

        currentSearchResult = GameMgr.Social.SearchPlayerById(targetPlayerId);
        RefreshSearchResult();
    }

    private void RefreshSearchResult()
    {
        if (addFriendItem != null)
        {
            addFriendItem.Refresh(this, currentSearchResult);
        }
    }

    private void ClearSearchResult()
    {
        currentSearchResult = null;
        if (addFriendItem != null)
        {
            addFriendItem.Refresh(this, null);
        }
    }

    private void Close()
    {
        ClearSearchResult();
        GameMgr.UI?.HidePanel<AddFriendPanel>();
    }

    private string GetInputText()
    {
        if (tmpFriendIdField != null)
        {
            return tmpFriendIdField.text;
        }

        return legacyFriendIdField != null ? legacyFriendIdField.text : string.Empty;
    }

    private void SetInputText(string value)
    {
        if (tmpFriendIdField != null)
        {
            tmpFriendIdField.SetTextWithoutNotify(value);
        }

        if (legacyFriendIdField != null)
        {
            legacyFriendIdField.SetTextWithoutNotify(value);
        }
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }
}
