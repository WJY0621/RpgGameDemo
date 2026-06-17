using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageItem : MonoBehaviour
{
    [SerializeField] private float maxTextWidth = 360f;

    private RectTransform itemRect;
    private GameObject leftRoot;
    private GameObject rightRoot;
    private RectTransform leftBubbleRect;
    private RectTransform rightBubbleRect;
    private TMP_Text leftMessageText;
    private TMP_Text rightMessageText;
    private TMP_Text friendNameText;
    private TMP_Text mySelfText;
    private Image friendIcon;
    private Image playerIcon;
    private bool initialized;

    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        itemRect = transform as RectTransform;

        Transform leftRootTransform = transform.Find("LeftRoot");
        Transform rightRootTransform = transform.Find("RightRoot");
        Transform leftBubble = transform.Find("LeftRoot/LeftBubble");
        Transform rightBubble = transform.Find("RightRoot/RightBubble");

        leftRoot = leftRootTransform != null ? leftRootTransform.gameObject : null;
        rightRoot = rightRootTransform != null ? rightRootTransform.gameObject : null;
        leftBubbleRect = leftBubble as RectTransform;
        rightBubbleRect = rightBubble as RectTransform;
        leftMessageText = GetText(transform.Find("LeftRoot/LeftBubble/LeftMessageText"));
        rightMessageText = GetText(transform.Find("RightRoot/RightBubble/RightMessageText"));
        friendNameText = GetText(transform.Find("LeftRoot/FriendNameText"));
        mySelfText = GetText(transform.Find("RightRoot/MySelf"));
        friendIcon = GetImage(transform.Find("LeftRoot/FriendIcon"));
        playerIcon = GetImage(transform.Find("RightRoot/PlayerIcon"));
    }

    public void SetMessage(
        ChatMessageData message,
        string myPlayerId,
        string friendDisplayName,
        string myDisplayName)
    {
        Init();
        if (message == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isMine = string.Equals(message.senderPlayerId, myPlayerId, System.StringComparison.OrdinalIgnoreCase);
        SetRootActive(leftRoot, !isMine);
        SetRootActive(rightRoot, isMine);

        if (isMine)
        {
            SetText(rightMessageText, message.messageText);
            SetText(mySelfText, string.IsNullOrWhiteSpace(myDisplayName) ? "Me" : myDisplayName);
            SocialAvatarIcon.ApplyDefault(playerIcon);
            ApplyBubbleLayout(rightMessageText, rightBubbleRect);
        }
        else
        {
            SetText(leftMessageText, message.messageText);
            SetText(friendNameText, string.IsNullOrWhiteSpace(friendDisplayName) ? message.senderPlayerId : friendDisplayName);
            SocialAvatarIcon.ApplyDefault(friendIcon);
            ApplyBubbleLayout(leftMessageText, leftBubbleRect);
        }

        if (itemRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
        }
    }

    private void ApplyBubbleLayout(TMP_Text messageText, RectTransform bubbleRect)
    {
        if (messageText == null)
        {
            return;
        }

        string content = messageText.text ?? string.Empty;
        messageText.enableWordWrapping = false;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.ForceMeshUpdate();

        Vector2 singleLineSize = messageText.GetPreferredValues(content);
        float targetWidth = Mathf.Clamp(singleLineSize.x, 1f, maxTextWidth);

        messageText.enableWordWrapping = true;
        messageText.overflowMode = TextOverflowModes.Overflow;

        RectTransform textRect = messageText.transform as RectTransform;
        if (textRect != null)
        {
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }

        LayoutElement layoutElement = messageText.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = messageText.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = targetWidth;
        layoutElement.flexibleWidth = 0f;

        messageText.ForceMeshUpdate();

        if (bubbleRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
        }
    }

    private static TMP_Text GetText(Transform target)
    {
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Image GetImage(Transform target)
    {
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static void SetText(TMP_Text target, string content)
    {
        if (target != null)
        {
            target.text = content ?? string.Empty;
        }
    }

    private static void SetRootActive(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }
}
