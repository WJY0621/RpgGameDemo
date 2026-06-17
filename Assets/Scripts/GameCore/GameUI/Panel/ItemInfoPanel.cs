using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfoPanel : BasePanel
{
    public enum InfoOwner
    {
        None,
        PackageHover,
        EquipHover,
        EquipSelected
    }

    private Transform UIItemName;
    private Transform UIItemTextBG;
    private Transform UIItemInfo;
    private Transform UIItemDescription;
    private Transform UIItemInfoText;
    private Transform UIItemDescriptionText;
    private Transform UIUseButton;
    private Transform UIDiscardButton;
    private InfoOwner currentOwner = InfoOwner.None;

    private const float ItemDescriptionMinTextHeight = 60f;
    private const float SkillInfoMinTextHeight = 80f;
    private const float SectionPaddingHeight = 20f;

    public bool IsLockedByEquipSelection => currentOwner == InfoOwner.EquipSelected;
    public InfoOwner CurrentOwner => currentOwner;

    public override void Init()
    {
        InitUIName();
        InitClick();
        ConfigureAsDisplayOnly();
    }

    public override void Hide(UnityEngine.Events.UnityAction callBack = null)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        currentOwner = InfoOwner.None;
        callBack?.Invoke();
    }

    public void UpdatePanelInfo(Item item)
    {
        if (item == null)
        {
            return;
        }

        if (UIItemName == null)
        {
            InitUIName();
        }

        SetText(UIItemName, item.name);
        SetText(UIItemDescriptionText, item.description);
        SetText(UIItemInfoText, item.functionDescription);
        UpdateSkillInfoVisibility(item);
        RefreshDynamicSectionHeights();
    }

    public bool CanBeOverriddenBy(InfoOwner owner)
    {
        return true;
    }

    public void SetOwner(InfoOwner owner)
    {
        currentOwner = owner;
    }

    public bool TryHideFrom(InfoOwner owner)
    {
        if (currentOwner != owner)
        {
            return false;
        }

        GameMgr.UI.HidePanel<ItemInfoPanel>();
        return true;
    }

    private static void SetText(Transform trans, string content)
    {
        if (trans == null)
        {
            return;
        }

        TextMeshProUGUI tmp = trans.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = content;
            return;
        }

        Text txt = trans.GetComponent<Text>();
        if (txt != null)
        {
            txt.text = content;
        }
    }

    private void InitUIName()
    {
        UIItemName = transform.Find("ItemName");
        UIItemTextBG = transform.Find("ItemTextBG");
        UIItemDescription = transform.Find("ItemTextBG/ItemDescription");
        UIItemDescriptionText = transform.Find("ItemTextBG/ItemDescription/ItemDescriptionText");
        UIItemInfo = transform.Find("ItemTextBG/SkillInfo");
        UIItemInfoText = transform.Find("ItemTextBG/SkillInfo/SkillInfoText");
        UIUseButton = transform.Find("UseButton");
        UIDiscardButton = transform.Find("DiscardButton");
    }

    private void RefreshDynamicSectionHeights()
    {
        RefreshSectionHeight(UIItemDescription, UIItemDescriptionText, ItemDescriptionMinTextHeight);
        if (UIItemInfo != null && UIItemInfo.gameObject.activeSelf)
        {
            RefreshSectionHeight(UIItemInfo, UIItemInfoText, SkillInfoMinTextHeight);
        }

        if (UIItemTextBG != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(UIItemTextBG as RectTransform);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        Canvas.ForceUpdateCanvases();
    }

    private static void RefreshSectionHeight(Transform sectionRoot, Transform textRoot, float minTextHeight)
    {
        if (sectionRoot == null || textRoot == null)
        {
            return;
        }

        RectTransform sectionRect = sectionRoot as RectTransform;
        TextMeshProUGUI tmp = textRoot.GetComponent<TextMeshProUGUI>();

        if (sectionRect == null || tmp == null)
        {
            return;
        }

        tmp.ForceMeshUpdate();
        float preferredTextHeight = Mathf.Ceil(tmp.preferredHeight);
        float targetTextHeight = Mathf.Max(minTextHeight, preferredTextHeight);
        float targetSectionHeight = targetTextHeight + SectionPaddingHeight;

        sectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSectionHeight);
    }

    private void UpdateSkillInfoVisibility(Item item)
    {
        if (UIItemInfo == null)
        {
            return;
        }

        bool hasSkillInfo = item.itemType != ItemType.Material && !string.IsNullOrWhiteSpace(item.functionDescription);
        UIItemInfo.gameObject.SetActive(hasSkillInfo);
    }

    private void InitClick()
    {
        if (UIUseButton != null)
        {
            UIUseButton.GetComponent<Button>().onClick.AddListener(OnUseButtonClick);
        }

        if (UIDiscardButton != null)
        {
            UIDiscardButton.GetComponent<Button>().onClick.AddListener(OnDiscardButtonClick);
        }
    }

    private void OnDiscardButtonClick()
    {
    }

    private void OnUseButtonClick()
    {
    }

    private void ConfigureAsDisplayOnly()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }
}
