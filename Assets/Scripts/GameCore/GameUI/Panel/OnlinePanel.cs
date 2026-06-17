using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class OnlinePanel : BasePanel
{
    private TMP_Text friendNameText;
    private Button closeButton;
    private Button applyButton;
    private Button inviteButton;
    private FriendInfo currentFriend;
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
        friendNameText = GetText(transform.Find("FriendNameText"));
        closeButton = GetButton(transform.Find("CloseButton"));
        applyButton = GetButton(transform.Find("ApplyButton"));
        inviteButton = GetButton(transform.Find("InviteButton"));

        BindButton(closeButton, Close);
        BindButton(applyButton, OnClickApplyJoinWorld);
        BindButton(inviteButton, OnClickInviteToMyWorld);
    }

    public void OpenForFriend(FriendInfo friend)
    {
        currentFriend = friend != null ? friend.Clone() : null;
        if (friendNameText != null)
        {
            friendNameText.text = currentFriend != null ? currentFriend.displayName : string.Empty;
        }
    }

    private void OnClickApplyJoinWorld()
    {
        SendOnlineRequest(InviteType.RequestToJoinWorld);
    }

    private void OnClickInviteToMyWorld()
    {
        SendOnlineRequest(InviteType.InviteToMyWorld);
    }

    private void SendOnlineRequest(InviteType inviteType)
    {
        if (currentFriend == null || GameMgr.Social == null)
        {
            return;
        }

        SendOnlineRequestAsync(inviteType).Forget();
    }

    private async UniTaskVoid SendOnlineRequestAsync(InviteType inviteType)
    {
        bool sent = await GameMgr.Social.SendOnlineRequestAsync(currentFriend, inviteType);
        if (sent)
        {
            Close();
        }
    }

    private void Close()
    {
        GameMgr.UI?.HidePanel<OnlinePanel>();
    }

    private static TMP_Text GetText(Transform target)
    {
        return target != null ? target.GetComponent<TMP_Text>() : null;
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
}
