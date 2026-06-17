using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ApplyItemUI : MonoBehaviour
{
    private ApplyListPanel owner;
    private FriendRequestData requestData;
    private Image applyPlayerIcon;
    private TMP_Text tmpApplyPlayerName;
    private Text legacyApplyPlayerName;
    private Button refuseButton;
    private Button agreeButton;
    private bool initialized;

    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Transform iconTransform = transform.Find("ApplyPlayerIcon");
        Transform nameTransform = transform.Find("ApplyPlayerName");

        applyPlayerIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        tmpApplyPlayerName = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
        legacyApplyPlayerName = nameTransform != null ? nameTransform.GetComponent<Text>() : null;
        refuseButton = GetButton(transform.Find("RefuseButton"));
        agreeButton = GetButton(transform.Find("AgreeButton"));

        BindButton(refuseButton, OnClickRefuse);
        BindButton(agreeButton, OnClickAgree);
    }

    public void Refresh(ApplyListPanel panel, FriendRequestData request)
    {
        Init();
        owner = panel;
        requestData = request != null ? request.Clone() : null;

        string displayName = requestData != null && !string.IsNullOrWhiteSpace(requestData.fromDisplayName)
            ? requestData.fromDisplayName
            : requestData?.fromPlayerId ?? string.Empty;

        SetText(tmpApplyPlayerName, legacyApplyPlayerName, displayName);
        if (applyPlayerIcon != null)
        {
            SocialAvatarIcon.ApplyDefault(applyPlayerIcon);
        }

        gameObject.SetActive(requestData != null);
    }

    private void OnClickRefuse()
    {
        if (requestData == null)
        {
            return;
        }

        owner?.RefuseRequest(requestData);
    }

    private void OnClickAgree()
    {
        if (requestData == null)
        {
            return;
        }

        owner?.AcceptRequest(requestData);
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
}
