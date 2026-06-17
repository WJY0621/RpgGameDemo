using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipPanel : BasePanel
{
    private const string WeaponSlot1Key = "WeaponContent1";
    private const string WeaponSlot2Key = "WeaponContent2";

    private readonly Dictionary<string, EquipSlotUI> equipSlots = new Dictionary<string, EquipSlotUI>();

    private Transform equipContentRoot;
    private Transform closeButton;
    private TMP_Text roleInfoText;
    private Text legacyRoleInfoText;
    private EquipSlotUI draggingSlot;
    private Transform draggingIcon;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        equipContentRoot = transform.Find("EquipContent");
        closeButton = transform.Find("EquipContent/CloseButton") ?? transform.Find("CloseButton");
        Transform roleInfoTransform = transform.Find("RoleInfoBG/RoleInfoText");
        if (roleInfoTransform != null)
        {
            roleInfoText = roleInfoTransform.GetComponent<TMP_Text>();
            legacyRoleInfoText = roleInfoTransform.GetComponent<Text>();
        }

        if (closeButton != null)
        {
            Button button = closeButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickClose);
                button.onClick.AddListener(OnClickClose);
            }
        }

        BuildEquipSlots();
    }

    public override void Show()
    {
        base.Show();
        RefreshEquipSlots();
    }

    protected override void Update()
    {
        base.Update();
        MaintainHoverItemInfoVisibility();
    }

    public bool TryEquipItemToSlot(EquipSlotUI targetSlot, InventoryItem inventoryItem)
    {
        if (targetSlot == null || inventoryItem == null)
        {
            return false;
        }

        Item config = GameMgr.Package.GetItemConfig(inventoryItem.itemId);
        if (!targetSlot.CanEquip(config))
        {
            return false;
        }

        GameMgr.Package.SetEquippedItem(targetSlot.SlotKey, inventoryItem.uid);
        RefreshEquipSlots();
        RefreshPackagePanel();
        return true;
    }

    public bool TryEquipInventoryItem(InventoryItem inventoryItem)
    {
        if (inventoryItem == null)
        {
            return false;
        }

        Item config = GameMgr.Package.GetItemConfig(inventoryItem.itemId);
        if (config == null)
        {
            return false;
        }

        if (config is WeaponItem weaponItem &&
            weaponItem.equipSlot == EquipmentSlot.Weapon &&
            AreBothWeaponSlotsEquipped())
        {
            ShowWeaponReplaceTip(inventoryItem);
            return false;
        }

        EquipSlotUI firstCompatibleSlot = null;
        foreach (KeyValuePair<string, EquipSlotUI> pair in equipSlots)
        {
            EquipSlotUI slot = pair.Value;
            if (slot == null || !slot.CanEquip(config))
            {
                continue;
            }

            if (firstCompatibleSlot == null)
            {
                firstCompatibleSlot = slot;
            }

            if (!slot.HasEquippedItem())
            {
                return TryEquipItemToSlot(slot, inventoryItem);
            }
        }

        if (firstCompatibleSlot != null)
        {
            return TryEquipItemToSlot(firstCompatibleSlot, inventoryItem);
        }

        return false;
    }

    public bool TryEquipInventoryItemToSlotKey(string slotKey, InventoryItem inventoryItem)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || inventoryItem == null)
        {
            return false;
        }

        EquipSlotUI slotUI = GetSlotUI(slotKey);
        if (slotUI != null)
        {
            return TryEquipItemToSlot(slotUI, inventoryItem);
        }

        Item config = GameMgr.Package.GetItemConfig(inventoryItem.itemId);
        if (config is not WeaponItem weaponItem || weaponItem.equipSlot != ResolveAcceptedSlot(slotKey))
        {
            return false;
        }

        GameMgr.Package.SetEquippedItem(slotKey, inventoryItem.uid);
        RefreshEquipSlots();
        RefreshPackagePanel();
        return true;
    }

    private static bool AreBothWeaponSlotsEquipped()
    {
        return GameMgr.Package != null &&
               GameMgr.Package.GetEquippedInventoryItem(WeaponSlot1Key) != null &&
               GameMgr.Package.GetEquippedInventoryItem(WeaponSlot2Key) != null;
    }

    private async void ShowWeaponReplaceTip(InventoryItem inventoryItem)
    {
        EquipTipPanel tipPanel = await GameMgr.UI.ShowPanel<EquipTipPanel>();
        if (tipPanel == null)
        {
            Debug.LogError("[EquipPanel] Failed to show EquipTipPanel.");
            return;
        }

        tipPanel.ShowForWeaponReplacement(this, inventoryItem);
    }

    public void TryUnequipSlot(EquipSlotUI targetSlot)
    {
        if (targetSlot == null || !targetSlot.HasEquippedItem())
        {
            return;
        }

        GameMgr.Package.SetEquippedItem(targetSlot.SlotKey, null);

        RefreshEquipSlots();
        RefreshPackagePanel();
    }

    public void RefreshEquipSlots()
    {
        foreach (EquipSlotUI slot in equipSlots.Values)
        {
            slot.Refresh();
        }

        RefreshRoleInfo();
    }

    public EquipSlotUI GetSlotUI(string slotKey)
    {
        equipSlots.TryGetValue(slotKey, out EquipSlotUI slot);
        return slot;
    }

    private void BuildEquipSlots()
    {
        equipSlots.Clear();

        if (equipContentRoot == null)
        {
            return;
        }

        for (int i = 0; i < equipContentRoot.childCount; i++)
        {
            Transform slotTransform = equipContentRoot.GetChild(i);
            if (slotTransform.name.Equals("CloseButton", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string slotKey = slotTransform.name;
            EquipmentSlot acceptedSlot = ResolveAcceptedSlot(slotKey);

            EquipSlotUI slotUI = slotTransform.GetComponent<EquipSlotUI>();
            if (slotUI == null)
            {
                slotUI = slotTransform.gameObject.AddComponent<EquipSlotUI>();
            }

            slotUI.Setup(this, slotKey, acceptedSlot);
            equipSlots[slotKey] = slotUI;
        }
    }

    private static EquipmentSlot ResolveAcceptedSlot(string slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return EquipmentSlot.None;
        }

        if (slotKey.StartsWith("Head", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Head;
        }

        if (slotKey.StartsWith("Chest", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Chest;
        }

        if (slotKey.StartsWith("Leg", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Leg;
        }

        if (slotKey.StartsWith("Weapon", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Weapon;
        }

        if (slotKey.StartsWith("Tool", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Tool;
        }

        if (slotKey.StartsWith("Access", System.StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentSlot.Accessory;
        }

        return EquipmentSlot.None;
    }

    private void OnClickClose()
    {
        GameMgr.UI.HidePanel<EquipPanel>();
    }

    public void HandleSlotPointerEnter(EquipSlotUI slotUI)
    {
        if (slotUI == null || !slotUI.HasEquippedItem())
        {
            return;
        }

        ShowItemInfo(slotUI);
    }

    public void HandleSlotPointerExit(EquipSlotUI slotUI)
    {
        HideItemInfo();
    }

    private async void ShowItemInfo(EquipSlotUI slotUI)
    {
        if (slotUI == null)
        {
            return;
        }

        Item item = slotUI.GetEquippedItem();
        if (item == null)
        {
            HideItemInfo();
            return;
        }

        ItemInfoPanel infoPanel = await GameMgr.UI.ShowPanel<ItemInfoPanel>();
        if (infoPanel == null)
        {
            return;
        }

        infoPanel.SetOwner(ItemInfoPanel.InfoOwner.EquipHover);
        infoPanel.UpdatePanelInfo(item);
        infoPanel.transform.SetAsLastSibling();
        PositionItemInfoPanel(slotUI, infoPanel);
    }

    private static void HideItemInfo()
    {
        ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
        if (infoPanel != null && infoPanel.CurrentOwner == ItemInfoPanel.InfoOwner.EquipHover)
        {
            infoPanel.TryHideFrom(ItemInfoPanel.InfoOwner.EquipHover);
        }
    }

    private static void PositionItemInfoPanel(EquipSlotUI slotUI, ItemInfoPanel infoPanel)
    {
        if (slotUI == null || infoPanel == null)
        {
            return;
        }

        RectTransform slotRect = slotUI.GetComponent<RectTransform>();
        RectTransform infoRect = infoPanel.GetComponent<RectTransform>();
        if (slotRect == null || infoRect == null)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);

        bool placeRight = slotRect.position.x <= Screen.width * 0.5f;
        infoRect.pivot = new Vector2(placeRight ? 0f : 1f, 1f);
        infoPanel.transform.position = placeRight ? corners[3] : corners[2];
    }

    private static void RefreshPackagePanel()
    {
        PackagePanel packagePanel = GameMgr.UI.GetPanelWithoutLoad<PackagePanel>();
        if (packagePanel != null && packagePanel.gameObject.activeInHierarchy)
        {
            packagePanel.RefreshUi();
        }
    }

    private void RefreshRoleInfo()
    {
        PlayerData playerData = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (playerData == null)
        {
            SetRoleInfoText(string.Empty);
            return;
        }

        GameMgr.Equipment?.ApplyEquipmentStatsToPlayerData(playerData);

        string content =
            $"{playerData.GetMaxHP()}\n" +
            $"{playerData.GetATK()}\n" +
            $"{playerData.GetDEF()}\n" +
            $"{FormatPercent(playerData.GetCritRate())}\n" +
            $"{FormatPercent(playerData.GetCritDamage())}\n" +
            $"{FormatMultiplier(playerData.GetMoveSpeedMultiplier())}\n" +
            $"{FormatMultiplier(playerData.GetAttackSpeedMultiplier())}";

        SetRoleInfoText(content);
    }

    private void SetRoleInfoText(string content)
    {
        if (roleInfoText != null)
        {
            roleInfoText.text = content;
        }

        if (legacyRoleInfoText != null)
        {
            legacyRoleInfoText.text = content;
        }
    }

    private static string FormatPercent(float value)
    {
        return $"{value:0.##}%";
    }

    private static string FormatMultiplier(float value)
    {
        return $"{value:0.##}倍";
    }

    public async void OnBeginDragSlot(EquipSlotUI slotUI)
    {
        if (slotUI == null || !slotUI.HasEquippedItem())
        {
            return;
        }

        HideItemInfo();
        Item equippedItem = slotUI.GetEquippedItem();
        if (equippedItem == null)
        {
            return;
        }

        Sprite iconSprite = await GameMgr.IconAtlas.GetItemIcon(equippedItem);
        if (iconSprite == null)
        {
            return;
        }

        draggingSlot = slotUI;

        GameObject dragObj = new GameObject("DraggingEquipItem", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        dragObj.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.parent, false);
        dragObj.transform.SetAsLastSibling();

        RectTransform dragRect = dragObj.GetComponent<RectTransform>();
        RectTransform sourceRect = slotUI.GetComponent<RectTransform>();
        dragRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragRect.pivot = new Vector2(0.5f, 0.5f);
        dragRect.sizeDelta = sourceRect.sizeDelta;
        dragRect.position = sourceRect.position;
        dragRect.localScale = Vector3.one;

        Image dragImage = dragObj.GetComponent<Image>();
        dragImage.sprite = iconSprite;
        dragImage.preserveAspect = true;
        dragImage.color = Color.white;
        dragImage.raycastTarget = false;

        CanvasGroup canvasGroup = dragObj.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0.92f;

        draggingIcon = dragObj.transform;
        slotUI.SetEquipIconVisible(false);
    }

    public void OnDragSlot(PointerEventData eventData)
    {
        if (draggingIcon == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                draggingIcon.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector3 globalMousePos))
        {
            draggingIcon.position = globalMousePos;
        }
    }

    public void OnEndDragSlot(PointerEventData eventData)
    {
        if (draggingSlot == null)
        {
            ClearDraggingSlotState();
            return;
        }

        EquipSlotUI sourceSlot = draggingSlot;
        sourceSlot.SetEquipIconVisible(true);

        PackageItemUI targetUI = ResolveInventoryDropTarget(eventData);
        bool moved = false;
        if (targetUI != null)
        {
            moved = GameMgr.Package.MoveEquippedItemToInventorySlot(sourceSlot.SlotKey, targetUI.ItemIndex);
        }

        if (moved)
        {
            RefreshEquipSlots();
            RefreshPackagePanel();
        }
        else
        {
            RefreshEquipSlots();
        }

        ClearDraggingSlotState();
    }

    private static PackageItemUI ResolveInventoryDropTarget(PointerEventData eventData)
    {
        if (EventSystem.current == null || eventData == null)
        {
            return null;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            PackageItemUI hitItem = results[i].gameObject != null
                ? results[i].gameObject.GetComponentInParent<PackageItemUI>()
                : null;
            if (hitItem != null)
            {
                return hitItem;
            }
        }

        return null;
    }

    private void ClearDraggingSlotState()
    {
        if (draggingIcon != null)
        {
            Destroy(draggingIcon.gameObject);
            draggingIcon = null;
        }

        draggingSlot = null;
    }

    private void MaintainHoverItemInfoVisibility()
    {
        ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
        if (infoPanel == null || infoPanel.CurrentOwner != ItemInfoPanel.InfoOwner.EquipHover)
        {
            return;
        }

        if (IsPointerOverEquippedSlot())
        {
            return;
        }

        infoPanel.TryHideFrom(ItemInfoPanel.InfoOwner.EquipHover);
    }

    private bool IsPointerOverEquippedSlot()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (RaycastResult result in results)
        {
            EquipSlotUI slotUI = result.gameObject.GetComponentInParent<EquipSlotUI>();
            if (slotUI != null && slotUI.HasEquippedItem())
            {
                return true;
            }
        }

        return false;
    }
}
