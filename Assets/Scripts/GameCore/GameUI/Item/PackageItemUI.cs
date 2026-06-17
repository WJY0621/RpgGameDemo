using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackageItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const string HoverSoundName = "UI_Click2";

    private Image BK;
    private Transform UIIcon;
    private Transform UISelect;
    private Transform UIDeleteSelect;
    private Transform UINumbel;
    private Transform UINew;
    private Transform UIArtifactImage;

    private Item packageItem;
    public Item PackageItem => packageItem;

    private InventoryItem inventoryItem;
    public InventoryItem InventoryItem => inventoryItem;

    private PackagePanel uiParent;
    private int itemIndex;
    public int ItemIndex => itemIndex;

    private bool isSelected;
    private bool isDeleteSelected;
    public bool IsDeleteSelected => isDeleteSelected;

    private void Awake()
    {
        BK = GetComponent<Image>();
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("ItemImage");
        UISelect = transform.Find("Select");
        UIDeleteSelect = transform.Find("DeleteSelect");
        UINumbel = transform.Find("Numbel");
        UINew = transform.Find("New");
        UIArtifactImage = transform.Find("ArtifactImage");

        if (UISelect != null) UISelect.gameObject.SetActive(false);
        if (UIDeleteSelect != null) UIDeleteSelect.gameObject.SetActive(false);
        if (UINew != null) UINew.gameObject.SetActive(false);
        if (UIArtifactImage != null) UIArtifactImage.gameObject.SetActive(false);
    }

    public void Refresh(InventoryItem invItem, PackagePanel panel, int index)
    {
        inventoryItem = invItem;
        uiParent = panel;
        itemIndex = index;
        isSelected = false;
        isDeleteSelected = false;

        ResetUI();

        if (invItem == null)
        {
            packageItem = null;
            if (BK != null)
            {
                BK.color = new Color(0.15f, 0.22f, 0.35f, 0.4f);
            }

            return;
        }

        packageItem = GameMgr.Package.GetItemConfig(invItem.itemId);
        if (packageItem == null)
        {
            return;
        }

        UpdateItem();
    }

    /// <summary>
    /// 拖拽开始/结束时调用：隐藏或恢复整个槽位的视觉（图标 + 背景色）。
    /// 结束后 PackagePanel 会调用 RefreshUi 做完整刷新，此处仅做即时视觉反馈。
    /// </summary>
    public void SetDraggingState(bool isDragging)
    {
        if (BK != null)
        {
            BK.color = GetCurrentBKColor();
        }

        if (UIIcon != null && packageItem != null)
        {
            UIIcon.gameObject.SetActive(!isDragging);
        }

        if (UINumbel != null)
        {
            bool showCount = !isDragging && packageItem != null && inventoryItem != null && packageItem.itemType != ItemType.Weapon && inventoryItem.count > 1;
            UINumbel.gameObject.SetActive(showCount);
        }

        if (UINew != null)
        {
            bool showNew = !isDragging && inventoryItem != null && inventoryItem.isNew;
            UINew.gameObject.SetActive(showNew);
        }

        if (UIArtifactImage != null)
        {
            UIArtifactImage.gameObject.SetActive(!isDragging && IsArtifactWeapon());
        }
    }

    private Color GetCurrentBKColor()
    {
        if (inventoryItem == null || packageItem == null)
        {
            return new Color(0.15f, 0.22f, 0.35f, 0.4f);
        }

        if (isDeleteSelected)
        {
            return new Color(0.5f, 0.1f, 0.1f, 1f);
        }

        return packageItem.quality switch
        {
            ItemQuality.Common => new Color(0.24f, 0.28f, 0.35f, 1f),
            ItemQuality.Advanced => new Color(0.12f, 0.45f, 0.12f, 1f),
            ItemQuality.Rare => new Color(0.12f, 0.35f, 0.65f, 1f),
            ItemQuality.Epic => new Color(0.45f, 0.12f, 0.65f, 1f),
            ItemQuality.Legendary => new Color(0.75f, 0.45f, 0.12f, 1f),
            _ => new Color(0.24f, 0.28f, 0.35f, 1f)
        };
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        uiParent?.OnBeginDragItem(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        uiParent?.OnDragItem(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        uiParent?.OnEndDragItem(eventData);
    }

    private void ResetUI()
    {
        if (UIIcon != null) UIIcon.gameObject.SetActive(false);
        if (UINumbel != null) UINumbel.gameObject.SetActive(false);
        if (UINew != null) UINew.gameObject.SetActive(false);
        if (UIArtifactImage != null) UIArtifactImage.gameObject.SetActive(false);
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
        if (UIIcon == null || string.IsNullOrEmpty(packageItem.iconName))
        {
            return;
        }

        Sprite sp = await GameMgr.IconAtlas.GetItemIcon(packageItem);
        if (sp != null && UIIcon != null)
        {
            UIIcon.GetComponent<Image>().sprite = sp;
            UIIcon.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[PackageItemUI] Failed to load icon: {packageItem.iconName} for item: {packageItem.name}");
        }
    }

    private void UpdateInfo()
    {
        if (UINumbel != null)
        {
            TextMeshProUGUI numTextTMP = UINumbel.GetComponent<TextMeshProUGUI>();
            Text numTextLegacy = UINumbel.GetComponent<Text>();

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
            UINew.gameObject.SetActive(inventoryItem.isNew);
        }

        if (UIArtifactImage != null)
        {
            UIArtifactImage.gameObject.SetActive(IsArtifactWeapon());
        }
    }

    private void UpdateBKModel()
    {
        if (BK == null)
        {
            return;
        }

        BK.color = GetCurrentBKColor();
    }

    private bool IsArtifactWeapon()
    {
        return packageItem is WeaponItem weaponItem && weaponItem.IsArtifactWeapon;
    }

    public void ToggleDeleteSelect()
    {
        isDeleteSelected = !isDeleteSelected;
        UpdateBKModel();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryItem == null || packageItem == null)
        {
            return;
        }

        if (uiParent != null && uiParent.IsInDeleteMode)
        {
            uiParent.OnItemClickedInDeleteMode(this, inventoryItem);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (packageItem is ConsumableItem)
        {
            if (GameMgr.Package != null && GameMgr.Package.UseConsumable(packageItem.id))
            {
                uiParent?.RefreshUi();
            }

            return;
        }

        if (packageItem is WeaponItem)
        {
            uiParent?.TryQuickEquipInventoryItem(inventoryItem);
        }
    }

    public void SetSelectState(bool selected)
    {
        isSelected = selected;
        if (UISelect != null)
        {
            UISelect.gameObject.SetActive(selected);
            if (selected)
            {
                UpdateSelectFrame();
            }
        }
    }

    private async void UpdateSelectFrame()
    {
        string spriteName = packageItem.quality switch
        {
            ItemQuality.Common => "select_normal",
            ItemQuality.Advanced => "select_advance",
            ItemQuality.Rare => "select_rare",
            ItemQuality.Epic => "select_epic",
            ItemQuality.Legendary => "select_legend",
            _ => "select_normal"
        };

        Sprite sp = await GameMgr.IconAtlas.GetPackageUISprite(spriteName);
        if (sp != null && UISelect != null)
        {
            UISelect.GetComponent<Image>().sprite = sp;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryItem == null || packageItem == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (uiParent != null && (uiParent.IsInDeleteMode || uiParent.IsDragging))
        {
            return;
        }

        GameMgr.Audio?.PlayUIEffect(HoverSoundName);

        if (UISelect != null)
        {
            UISelect.gameObject.SetActive(true);
            UpdateSelectFrame();
        }

        if (uiParent != null && inventoryItem != null)
        {
            ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
            if (infoPanel != null && !infoPanel.CanBeOverriddenBy(ItemInfoPanel.InfoOwner.PackageHover))
            {
                return;
            }

            uiParent.OnShowHoverInfo(inventoryItem, this);

            if (inventoryItem.isNew)
            {
                inventoryItem.isNew = false;
                if (UINew != null)
                {
                    UINew.gameObject.SetActive(false);
                }
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (UISelect != null)
        {
            UISelect.gameObject.SetActive(false);
        }

        ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
        if (infoPanel != null)
        {
            infoPanel.TryHideFrom(ItemInfoPanel.InfoOwner.PackageHover);
        }
    }
}
