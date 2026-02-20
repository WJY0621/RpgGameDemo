using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackageItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Image BK;
    private Transform UIIcon;
    private Transform UISelect;
    private Transform UIDeleteSelect;
    private Transform UINumbel;
    private Transform UINew;

    private Item packageItem;
    public Item PackageItem => packageItem;
    private InventoryItem inventoryItem;
    private PackagePanel uiParent;
    private int itemIndex; // 在列表中的索引
    public int ItemIndex => itemIndex;
    private Coroutine hoverCoroutine;

    void Awake()
    {
        BK = this.GetComponent<Image>();
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("ItemImage");
        UISelect = transform.Find("Select");
        UIDeleteSelect = transform.Find("DeleteSelect");
        UINumbel = transform.Find("Numbel");
        UINew = transform.Find("New");

        if (UISelect != null) UISelect.gameObject.SetActive(false);
        if (UIDeleteSelect != null) UIDeleteSelect.gameObject.SetActive(false);
        if (UINew != null) UINew.gameObject.SetActive(false);
    }

    private bool isSelected = false;
    private bool isDeleteSelected = false; // 是否被批量删除选中
    public bool IsDeleteSelected => isDeleteSelected;

    public void Refresh(InventoryItem invItem, PackagePanel panel, int index)
    {
        this.inventoryItem = invItem;
        this.uiParent = panel;
        this.itemIndex = index;
        this.isSelected = false; 
        this.isDeleteSelected = false; // 重置批量删除选中状态
        
        // 在开始加载新数据前，先清空/隐藏当前 UI 表现
        ResetUI();

        // 如果传入的 invItem 为空，说明这是一个“空白格”
        if (invItem == null)
        {
            this.packageItem = null;
            // 空白格背景设为更亮的深蓝色半透明
            if (BK != null) BK.color = new Color(0.15f, 0.22f, 0.35f, 0.4f);
            return;
        }

        this.packageItem = GameMgr.Package.GetItemConfig(invItem.itemId);

        if (packageItem == null)
        {
            return;
        }

        UpdateItem();
    }

    public void SetIconVisible(bool visible)
    {
        if (UIIcon != null && packageItem != null)
        {
            UIIcon.gameObject.SetActive(visible);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (uiParent != null) uiParent.OnBeginDragItem(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (uiParent != null) uiParent.OnDragItem(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 射线检测当前落点下的格子
        PackageItemUI target = null;
        if (eventData.pointerEnter != null)
        {
            target = eventData.pointerEnter.GetComponentInParent<PackageItemUI>();
        }
        
        if (uiParent != null) uiParent.OnEndDragItem(target);
    }

    private void ResetUI()
    {
        if (UIIcon != null) UIIcon.gameObject.SetActive(false);
        if (UINumbel != null) UINumbel.gameObject.SetActive(false);
        if (UINew != null) UINew.gameObject.SetActive(false);
        if (UISelect != null) UISelect.gameObject.SetActive(false);
        if (BK != null) BK.color = Color.white;
    }

    private void UpdateItem()
    {
        UpdateBKModel();
        UpdateIcon();
        UpdateInfo();
        SetSelectState(isSelected);
    }
    
    private async void UpdateIcon()
    {
        if (UIIcon != null && !string.IsNullOrEmpty(packageItem.iconName))
        {
            // 直接使用配置的iconName加载Addressable资源
            Sprite sp = await GameMgr.AssetLoader.LoadAsset<Sprite>(packageItem.iconName);

            if (sp != null && UIIcon != null)
            {
                UIIcon.GetComponent<Image>().sprite = sp;
                // 确保图片组件是激活的
                UIIcon.gameObject.SetActive(true);
            }
            else
            {
                 Debug.LogWarning($"[PackageItemUI] Failed to load icon: {packageItem.iconName} for item: {packageItem.name}");
            }
        }
    }

    private void UpdateInfo()
    {
        if (UINumbel != null)
        {
            var numTextTMP = UINumbel.GetComponent<TextMeshProUGUI>();
            var numTextLegacy = UINumbel.GetComponent<Text>();

            if (packageItem.itemType != ItemType.Weapon && inventoryItem.count > 1)
            {
                string countStr = inventoryItem.count.ToString();
                if (numTextTMP != null) numTextTMP.text = countStr;
                else if (numTextLegacy != null) numTextLegacy.text = countStr;
                
                UINumbel.gameObject.SetActive(true);
            }
            else
            {
                UINumbel.gameObject.SetActive(false);
            }
        }

        if (UINew != null)
        {
            // 修复 isNew 显示逻辑：只有显式为 true 时才显示
            UINew.gameObject.SetActive(inventoryItem.isNew == true);
        }
    }

    private void UpdateBKModel()
    {
        if (BK == null) return;

        // 如果被批量删除选中，背景变暗红色
        if (isDeleteSelected)
        {
            BK.color = new Color(0.5f, 0.1f, 0.1f, 1f); // 暗红色
            return;
        }

        // 根据 Quality 设置背景颜色 (使用更鲜明的颜色以示区别)
        switch (packageItem.quality)
        {
            case ItemQuality.Common:
                BK.color = new Color(0.24f, 0.28f, 0.35f, 1f); // 灰蓝色 (普通)
                break;
            case ItemQuality.Advanced:
                BK.color = new Color(0.12f, 0.45f, 0.12f, 1f); // 翠绿色 (高级)
                break;
            case ItemQuality.Rare:
                BK.color = new Color(0.12f, 0.35f, 0.65f, 1f); // 宝石蓝 (稀有)
                break;
            case ItemQuality.Epic:
                BK.color = new Color(0.45f, 0.12f, 0.65f, 1f); // 幻想紫 (史诗)
                break;
            case ItemQuality.Legendary:
                BK.color = new Color(0.75f, 0.45f, 0.12f, 1f); // 传说的橙色 (传说)
                break;
            default:
                BK.color = new Color(0.24f, 0.28f, 0.35f, 1f);
                break;
        }
    }

    public void ToggleDeleteSelect()
    {
        isDeleteSelected = !isDeleteSelected;
        UpdateBKModel();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 如果是空白格，点击无效
        if (inventoryItem == null || packageItem == null) return;

        // 如果当前是批量删除模式
        if (uiParent != null && uiParent.IsInDeleteMode)
        {
            uiParent.OnItemClickedInDeleteMode(this, inventoryItem);
            return;
        }

        // 移除点击选中逻辑
    }

    public void SetSelectState(bool isSelected)
    {
        this.isSelected = isSelected;
        if (UISelect != null)
        {
            UISelect.gameObject.SetActive(isSelected);
            if (isSelected)
            {
                UpdateSelectFrame();
            }
        }
    }

    private async void UpdateSelectFrame()
    {
        string spriteName = "select_normal";
        switch (packageItem.quality)
        {
            case ItemQuality.Common: spriteName = "select_normal"; break;
            case ItemQuality.Advanced: spriteName = "select_advance"; break;
            case ItemQuality.Rare: spriteName = "select_rare"; break;
            case ItemQuality.Epic: spriteName = "select_epic"; break;
            case ItemQuality.Legendary: spriteName = "select_legend"; break; 
            default: spriteName = "select_normal"; break;
        }

        Sprite sp = await GameMgr.AssetLoader.LoadAsset<Sprite>(spriteName);
        if (sp != null && UISelect != null)
        {
            UISelect.GetComponent<Image>().sprite = sp;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 如果是空白格，悬停无效
        if (inventoryItem == null || packageItem == null) return;

        // 批量删除模式下不显示详情面板
        if (uiParent != null && uiParent.IsInDeleteMode) return;

        // 1. 立即显示选中框
        if (UISelect != null)
        {
            UISelect.gameObject.SetActive(true);
            UpdateSelectFrame();
        }

        // 2. 立即显示信息面板 (不再使用协程延迟)
        if (uiParent != null && inventoryItem != null)
        {
            // 触发显示信息面板，但不改变“点击选中”状态
            uiParent.OnShowHoverInfo(inventoryItem, this);
            
            // 如果是新物品，看一眼后就重置 isNew 状态
            if (inventoryItem.isNew)
            {
                inventoryItem.isNew = false;
                if (UINew != null) UINew.gameObject.SetActive(false);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 1. 隐藏选中框
        if (UISelect != null) 
        {
            UISelect.gameObject.SetActive(false);
        }

        // 2. 关闭信息面板 (如果正在显示)
        GameMgr.UI.HidePanel<ItemInfoPanel>();
    }

    private void OnDisable()
    {
        // 销毁或禁用时清理 (这里不再需要清理协程，但保留方法结构以防后续添加其他逻辑)
    }
}
