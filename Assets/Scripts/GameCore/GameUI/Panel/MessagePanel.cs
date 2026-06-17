using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessagePanel : BasePanel
{
    private const float BackgroundHorizontalPadding = 40f;
    private const float BackgroundVerticalPadding = 12f;
    private const float HiddenOffset = 50f;

    private bool initialized;
    private RectTransform rootRect;
    private RectTransform bgRect;
    private RectTransform textRect;
    private Image backgroundImage;
    private TMP_Text messageText;
    private Vector2 shownAnchoredPosition;
    private float preferredHeight;
    private string pendingContent = string.Empty;
    private int pendingRefreshFrames;
    private static TMP_FontAsset chineseFallbackFont;

    public CanvasGroup PanelCanvasGroup => GetComponent<CanvasGroup>();
    public float CurrentAlpha => PanelCanvasGroup != null ? PanelCanvasGroup.alpha : 0f;
    public float PreferredHeight => preferredHeight > 0f ? preferredHeight : (bgRect != null ? bgRect.rect.height : 0f);
    public Vector2 ShownAnchoredPosition => shownAnchoredPosition;

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

        rootRect = transform as RectTransform;
        Transform bg = transform.Find("BG");
        Transform text = transform.Find("BG/MessageText");

        bgRect = bg as RectTransform;
        textRect = text as RectTransform;
        backgroundImage = bg != null ? bg.GetComponent<Image>() : null;
        messageText = text != null ? text.GetComponent<TMP_Text>() : null;

        if (messageText != null)
        {
            messageText.enableWordWrapping = false;
            messageText.overflowMode = TextOverflowModes.Overflow;
            EnsureChineseFallbackFont();
        }

        if (rootRect != null)
        {
            shownAnchoredPosition = rootRect.anchoredPosition;
        }

        if (PanelCanvasGroup != null)
        {
            PanelCanvasGroup.alpha = 0f;
        }

        UpdateSlidePosition();
    }

    public override void Show()
    {
        gameObject.SetActive(true);
        base.Show();
        QueueLayoutRefresh();
        UpdateSlidePosition();
    }

    protected override void Update()
    {
        base.Update();
        ProcessDeferredRefresh();
        UpdateSlidePosition();
    }

    public void SetMessage(string content)
    {
        Init();
        pendingContent = content ?? string.Empty;

        if (messageText == null || textRect == null || bgRect == null)
        {
            return;
        }

        ApplyMessageLayout();
        QueueLayoutRefresh();
        UpdateSlidePosition();
    }

    public void SetShownAnchoredPosition(Vector2 anchoredPosition)
    {
        Init();
        shownAnchoredPosition = anchoredPosition;
        UpdateSlidePosition();
    }

    private void UpdateSlidePosition()
    {
        if (rootRect == null || bgRect == null)
        {
            return;
        }

        float alpha = CurrentAlpha;
        float hiddenX = shownAnchoredPosition.x - bgRect.rect.width - HiddenOffset;
        Vector2 hiddenPosition = new Vector2(hiddenX, shownAnchoredPosition.y);
        rootRect.anchoredPosition = Vector2.Lerp(hiddenPosition, shownAnchoredPosition, alpha);
    }

    private void ApplyMessageLayout()
    {
        if (messageText == null || textRect == null || bgRect == null)
        {
            return;
        }

        messageText.text = pendingContent;
        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        Vector2 preferredSize = messageText.GetPreferredValues(pendingContent);
        float textWidth = Mathf.Max(1f, preferredSize.x);
        float textHeight = Mathf.Max(messageText.fontSize + 4f, preferredSize.y);

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);

        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + BackgroundHorizontalPadding);
        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + BackgroundVerticalPadding);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bgRect);
        preferredHeight = textHeight + BackgroundVerticalPadding;
    }

    private void EnsureChineseFallbackFont()
    {
        if (messageText == null || messageText.font == null)
        {
            return;
        }

        if (chineseFallbackFont == null)
        {
            chineseFallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/MSYH SDF");
        }

        if (chineseFallbackFont == null || messageText.font == chineseFallbackFont)
        {
            return;
        }

        if (!messageText.font.fallbackFontAssetTable.Contains(chineseFallbackFont))
        {
            messageText.font.fallbackFontAssetTable.Add(chineseFallbackFont);
        }
    }

    private void QueueLayoutRefresh()
    {
        pendingRefreshFrames = 2;
    }

    private void ProcessDeferredRefresh()
    {
        if (pendingRefreshFrames <= 0)
        {
            return;
        }

        pendingRefreshFrames--;
        ApplyMessageLayout();
    }
}
