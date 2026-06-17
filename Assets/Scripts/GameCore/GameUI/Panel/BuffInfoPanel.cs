using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Buff hover 信息浮窗。预制体名 = "BuffInfoPanel"，通过 UIMgr (Addressables) 按需加载。
/// 结构：BuffNameText / Image / BuffInfoText / TimeText。
/// </summary>
public class BuffInfoPanel : BasePanel
{
    private RectTransform rect;
    private TMP_Text buffNameText;
    private TMP_Text buffInfoText;
    private TMP_Text timeText;
    private Image iconImage;
    private CanvasGroup cg;

    private BuffInstance currentInstance;

    public bool IsShowingFor(BuffInstance instance)
    {
        return gameObject.activeSelf && currentInstance == instance;
    }

    public override void Init()
    {
        rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            // pivot 设到左上角，方便定位到 icon 的"右下角"
            rect.pivot = new Vector2(0f, 1f);
        }

        cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Transform t;
        t = transform.Find("BuffNameText"); buffNameText = t != null ? t.GetComponent<TMP_Text>() : null;
        t = transform.Find("BuffInfoText"); buffInfoText = t != null ? t.GetComponent<TMP_Text>() : null;
        t = transform.Find("TimeText");     timeText     = t != null ? t.GetComponent<TMP_Text>() : null;
        t = transform.Find("Image");        iconImage    = t != null ? t.GetComponent<Image>()    : null;

        gameObject.SetActive(false);
    }

    public void ShowFor(BuffInstance instance, RectTransform anchorIcon)
    {
        if (instance == null || anchorIcon == null) return;
        currentInstance = instance;
        BuffData data = instance.Data;

        if (buffNameText != null) buffNameText.text = data.buffName;
        if (buffInfoText != null)
        {
            buffInfoText.text = !string.IsNullOrWhiteSpace(data.description)
                ? data.description
                : BuffFormatter.GetSummary(data);
        }
        if (timeText != null) timeText.text = BuffFormatter.GetRemainingText(instance);

        LoadIcon(data.iconName, instance);

        gameObject.SetActive(true);
        if (cg != null) cg.alpha = 1f;
        transform.SetAsLastSibling();
        PositionAtIconBottomRight(anchorIcon);
    }

    public void HideTooltip()
    {
        currentInstance = null;
        if (cg != null) cg.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 覆盖 BasePanel.Update：不走 Show/Hide 的 alpha 渐隐渐现（基类的 isShow 一直是 false 会把 alpha 拉到 0）。
    /// 只负责显示状态下持续刷新剩余时间。
    /// </summary>
    protected override void Update()
    {
        if (!gameObject.activeSelf || currentInstance == null) return;
        if (timeText != null) timeText.text = BuffFormatter.GetRemainingText(currentInstance);
    }

    private async void LoadIcon(string iconName, BuffInstance owner)
    {
        if (iconImage == null) return;

        if (string.IsNullOrWhiteSpace(iconName))
        {
            iconImage.gameObject.SetActive(false);
            iconImage.sprite = null;
            return;
        }

        Sprite sprite = await GameMgr.IconAtlas.GetBuffIcon(iconName);
        if (currentInstance != owner || iconImage == null) return;

        iconImage.sprite = sprite;
        iconImage.gameObject.SetActive(sprite != null);
    }

    private void PositionAtIconBottomRight(RectTransform anchorIcon)
    {
        if (rect == null || anchorIcon == null) return;

        Vector3[] corners = new Vector3[4];
        anchorIcon.GetWorldCorners(corners);
        // 0=BL, 1=TL, 2=TR, 3=BR
        rect.position = corners[3];
    }
}
