using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RedDotView : MonoBehaviour
{
    private const float PureDotSize = 14f;
    private const float NumberDotHeight = 22f;
    private const float NumberDotMinWidth = 22f;

    [SerializeField] private RedDotType redDotType;
    [SerializeField] private bool showNumber;
    [SerializeField] private GameObject dotRoot;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private bool allowCreateVisual = true;
    [SerializeField] private bool preserveExistingLayout;
    private bool usingExistingVisual;
    private bool configured;

    public void Configure(RedDotType type, bool showCount)
    {
        Configure(type, showCount, true, true);
    }

    public void Configure(RedDotType type, bool showCount, bool allowCreate, bool preserveLayout)
    {
        redDotType = type;
        showNumber = showCount;
        allowCreateVisual = allowCreate;
        preserveExistingLayout = preserveLayout;
        configured = true;
        EnsureVisual();
        Refresh();
    }

    private void OnEnable()
    {
        if (GameMgr.RedDot != null)
        {
            GameMgr.RedDot.OnRedDotChanged -= HandleRedDotChanged;
            GameMgr.RedDot.OnRedDotChanged += HandleRedDotChanged;
        }

        if (configured)
        {
            EnsureVisual();
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (GameMgr.RedDot != null)
        {
            GameMgr.RedDot.OnRedDotChanged -= HandleRedDotChanged;
        }
    }

    private void HandleRedDotChanged(RedDotType changedType)
    {
        if (changedType == redDotType)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (GameMgr.RedDot == null)
        {
            SetVisible(false);
            return;
        }

        int count = GameMgr.RedDot.GetCount(redDotType);
        SetVisible(count > 0);
        RefreshNumber(count);
    }

    private void SetVisible(bool visible)
    {
        if (dotRoot != null)
        {
            dotRoot.SetActive(visible);
        }
    }

    private void RefreshNumber(int count)
    {
        if (countText == null)
        {
            return;
        }

        bool showText = showNumber && count > 0;
        countText.gameObject.SetActive(showText);
        if (showText)
        {
            countText.text = count > 99 ? "99+" : count.ToString();
        }

        RectTransform dotRect = dotRoot != null ? dotRoot.GetComponent<RectTransform>() : null;
        if (dotRect != null && !(preserveExistingLayout && usingExistingVisual))
        {
            dotRect.sizeDelta = showNumber
                ? new Vector2(count > 99 ? 32f : NumberDotMinWidth, NumberDotHeight)
                : new Vector2(PureDotSize, PureDotSize);
        }
    }

    private void EnsureVisual()
    {
        if (dotRoot == null)
        {
            Transform existing = transform.Find("RedDot");
            if (existing != null)
            {
                dotRoot = existing.gameObject;
                usingExistingVisual = true;
            }
            else if (allowCreateVisual)
            {
                dotRoot = CreateDotRoot();
                usingExistingVisual = false;
            }
            else
            {
                return;
            }
        }
        else if (!usingExistingVisual &&
                 dotRoot.transform != null &&
                 dotRoot.transform.parent == transform &&
                 dotRoot.name == "RedDot")
        {
            usingExistingVisual = true;
        }

        Image image = dotRoot.GetComponent<Image>();
        if (image == null)
        {
            image = dotRoot.AddComponent<Image>();
        }

        bool preserveLayout = preserveExistingLayout && usingExistingVisual;
        if (!preserveLayout)
        {
            image.color = new Color(0.92f, 0.08f, 0.08f, 1f);
        }

        RectTransform dotRect = dotRoot.GetComponent<RectTransform>();
        if (dotRect != null && !preserveLayout)
        {
            dotRect.anchorMin = new Vector2(1f, 1f);
            dotRect.anchorMax = new Vector2(1f, 1f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(-5f, -5f);
        }

        if (showNumber && countText == null)
        {
            countText = dotRoot.GetComponentInChildren<TMP_Text>(true);
            if (countText == null)
            {
                countText = CreateCountText(dotRoot.transform);
            }
        }

        if (!showNumber && countText != null)
        {
            countText.gameObject.SetActive(false);
        }
    }

    private GameObject CreateDotRoot()
    {
        GameObject root = new GameObject("RedDot", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        return root;
    }

    private static TMP_Text CreateCountText(Transform parent)
    {
        GameObject textObject = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 15f;
        text.raycastTarget = false;
        return text;
    }
}
