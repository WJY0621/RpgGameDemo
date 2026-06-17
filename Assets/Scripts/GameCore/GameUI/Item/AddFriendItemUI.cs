using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddFriendItemUI : MonoBehaviour
{
    private AddFriendPanel owner;
    private FriendInfo searchResult;
    private Image addFriendIcon;
    private TMP_Text tmpAddFriendName;
    private Text legacyAddFriendName;
    private Button addFriendButton;
    private bool initialized;

    public FriendInfo SearchResult => searchResult;

    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Transform iconTransform = transform.Find("AddFriendIcon");
        Transform nameTransform = transform.Find("AddFriendName");

        addFriendIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        tmpAddFriendName = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
        legacyAddFriendName = nameTransform != null ? nameTransform.GetComponent<Text>() : null;
        addFriendButton = GetButton(transform.Find("AddFriendButton"));

        if (addFriendButton != null)
        {
            addFriendButton.onClick.RemoveListener(OnClickAddFriend);
            addFriendButton.onClick.AddListener(OnClickAddFriend);
        }
    }

    public void Refresh(AddFriendPanel panel, FriendInfo result)
    {
        Init();
        owner = panel;
        searchResult = result != null ? result.Clone() : null;

        string displayName = searchResult != null && !string.IsNullOrWhiteSpace(searchResult.displayName)
            ? searchResult.displayName
            : searchResult?.playerId ?? string.Empty;

        SetText(tmpAddFriendName, legacyAddFriendName, displayName);
        if (addFriendIcon != null)
        {
            SocialAvatarIcon.ApplyDefault(addFriendIcon);
        }

        gameObject.SetActive(searchResult != null);
    }

    private void OnClickAddFriend()
    {
        if (searchResult == null)
        {
            return;
        }

        owner?.SendFriendRequest(searchResult);
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
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
