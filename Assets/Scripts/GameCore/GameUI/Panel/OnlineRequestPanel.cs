using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineRequestPanel : BasePanel
{
    private Image requestPlayerIcon;
    private TMP_Text tmpRequestPlayerNameText;
    private TMP_Text tmpRequestContentText;
    private Text legacyRequestPlayerNameText;
    private Text legacyRequestContentText;
    private Button refuseButton;
    private Button agreeButton;
    private InviteData currentInvite;
    private bool initialized;
    private bool acceptingInvite;

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
        Transform iconTransform = transform.Find("RequestPlayerIcon");
        Transform nameTransform = transform.Find("RequestPlayerNameText");
        Transform contentTransform = transform.Find("RequestContentText");

        requestPlayerIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        tmpRequestPlayerNameText = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
        legacyRequestPlayerNameText = nameTransform != null ? nameTransform.GetComponent<Text>() : null;
        tmpRequestContentText = contentTransform != null ? contentTransform.GetComponent<TMP_Text>() : null;
        legacyRequestContentText = contentTransform != null ? contentTransform.GetComponent<Text>() : null;
        refuseButton = GetButton(transform.Find("RefuseButton"));
        agreeButton = GetButton(transform.Find("AgreeButton"));

        BindButton(refuseButton, OnClickRefuse);
        BindButton(agreeButton, OnClickAgree);
    }

    public void Open(InviteData inviteData)
    {
        Init();
        currentInvite = inviteData != null ? inviteData.Clone() : null;
        RefreshView();
    }

    private void RefreshView()
    {
        if (currentInvite == null)
        {
            SetText(tmpRequestPlayerNameText, legacyRequestPlayerNameText, string.Empty);
            SetText(tmpRequestContentText, legacyRequestContentText, string.Empty);
            return;
        }

        string requestName = !string.IsNullOrWhiteSpace(currentInvite.requesterDisplayName)
            ? currentInvite.requesterDisplayName
            : currentInvite.requesterPlayerId;
        SetText(tmpRequestPlayerNameText, legacyRequestPlayerNameText, requestName);

        bool isJoinRequest = currentInvite.inviteType == InviteType.RequestToJoinWorld;
        SetText(
            tmpRequestContentText,
            legacyRequestContentText,
            isJoinRequest ? "申请加入你的世界" : "邀请你加入他的世界");
        SetTextColor(
            tmpRequestContentText,
            legacyRequestContentText,
            isJoinRequest ? new Color(0.25f, 0.9f, 0.35f) : new Color(0.25f, 0.55f, 1f));

        if (requestPlayerIcon != null)
        {
            SocialAvatarIcon.ApplyDefault(requestPlayerIcon);
        }
    }

    private void OnClickRefuse()
    {
        if (acceptingInvite)
        {
            return;
        }

        GameMgr.Social?.RefuseOnlineRequest(currentInvite);
        Close();
    }

    private void OnClickAgree()
    {
        AcceptInviteAsync().Forget();
    }

    private async UniTaskVoid AcceptInviteAsync()
    {
        if (currentInvite == null || GameMgr.Social == null || acceptingInvite)
        {
            return;
        }

        acceptingInvite = true;
        SetButtonsInteractable(false);

        LoadingPanel loadingPanel = null;
        bool success = false;

        try
        {
            loadingPanel = await ShowInviteLoadingPanelAsync();
            success = await GameMgr.Social.AcceptOnlineRequestAsync(currentInvite);
            if (loadingPanel != null)
            {
                loadingPanel.SetProgress(success ? 1f : 0.95f);
            }
        }
        finally
        {
            await HideInviteLoadingPanelAsync(loadingPanel);
            acceptingInvite = false;
            SetButtonsInteractable(true);
        }

        if (success)
        {
            Close();
        }
    }

    private async UniTask<LoadingPanel> ShowInviteLoadingPanelAsync()
    {
        if (GameMgr.UI == null)
        {
            return null;
        }

        LoadingPanel panel = await GameMgr.UI.ShowPanel<LoadingPanel>();
        if (panel == null)
        {
            return null;
        }

        panel.SetProgress(0.35f);
        panel.SetTip("正在加入好友世界...");
        await panel.WaitUntilFullyShownAsync();
        return panel;
    }

    private async UniTask HideInviteLoadingPanelAsync(LoadingPanel loadingPanel)
    {
        if (loadingPanel == null || GameMgr.UI == null)
        {
            return;
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        GameMgr.UI.HidePanel<LoadingPanel>(() =>
        {
            completionSource.TrySetResult();
        });
        await completionSource.Task;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (refuseButton != null)
        {
            refuseButton.interactable = interactable;
        }

        if (agreeButton != null)
        {
            agreeButton.interactable = interactable;
        }
    }

    private void Close()
    {
        GameMgr.UI?.HidePanel<OnlineRequestPanel>();
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
}
