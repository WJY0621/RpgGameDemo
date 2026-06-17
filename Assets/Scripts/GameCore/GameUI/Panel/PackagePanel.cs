using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackagePanel : BasePanel
{
    private const int MinSlots = 25;
    private const int RowSize = 5;
    private const string HeadSlotKey = "HeadContent";
    private const string ChestSlotKey = "ChestContent";
    private const string LegSlotKey = "LegContent";
    private const string WeaponSlot1Key = "WeaponContent1";
    private const string WeaponSlot2Key = "WeaponContent2";
    private const string ToolSlot1Key = "ToolContent1";
    private const string ToolSlot2Key = "ToolContent2";
    private const string AccessorySlot1Key = "AccessContent1";
    private const string AccessorySlot2Key = "AccessContent2";
    private const string AccessorySlot3Key = "AccessContent3";

    private Transform weaponTab;
    private Transform consumTab;
    private Transform materialTab;
    private PackageTabItemUI weaponTabItem;
    private PackageTabItemUI consumTabItem;
    private PackageTabItemUI materialTabItem;
    private Transform closeButton;
    private Transform sortButton;
    private Transform deleteButton;
    private Transform coinNum;
    private Transform scrollView;
    private Transform deletePanel;
    private Transform backButton;
    private Transform ensureButton;
    private Transform deleteNum;

    private TMP_Text coinNumText;
    private Text coinNumLegacyText;

    private readonly Dictionary<string, PackageItemUI> itemUIByUid = new Dictionary<string, PackageItemUI>();
    private readonly List<PackageItemUI> pooledItemUIs = new List<PackageItemUI>();
    private readonly List<InventoryItem> selectedDeleteItems = new List<InventoryItem>();

    private ItemType currentType = ItemType.Weapon;
    private bool isInDeleteMode;
    private int refreshVersion;

    private PackagePanelDragController dragController;
    private PackagePanelHoverController hoverController;

    public bool IsInDeleteMode => isInDeleteMode;
    public bool IsDragging => dragController != null && dragController.IsDragging;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        dragController = new PackagePanelDragController(this);
        hoverController = new PackagePanelHoverController(this);

        BindReferences();
        BindClicks();
        RefreshCurrentTab();
    }

    public override void Show()
    {
        base.Show();
        BindReferences();
        BindClicks();
        RefreshCurrentTab();
        RefreshCoinNum();
    }

    protected override void Update()
    {
        base.Update();
        hoverController?.MaintainVisibility();
    }

    public void RefreshUi()
    {
        RefreshCoinNum();
        RefreshScroll();
    }

    public void OnBeginDragItem(PackageItemUI ui)
    {
        dragController?.BeginDrag(ui);
    }

    public void OnDragItem(PointerEventData eventData)
    {
        dragController?.Drag(eventData);
    }

    public void OnEndDragItem(PointerEventData eventData)
    {
        dragController?.EndDrag(eventData);
    }

    public void OnShowHoverInfo(InventoryItem item, PackageItemUI itemUI)
    {
        hoverController?.Show(item, itemUI);
    }

    public void OnItemClickedInDeleteMode(PackageItemUI ui, InventoryItem item)
    {
        if (ui == null || item == null)
        {
            return;
        }

        if (selectedDeleteItems.Contains(item))
        {
            selectedDeleteItems.Remove(item);
        }
        else
        {
            selectedDeleteItems.Add(item);
        }

        ui.ToggleDeleteSelect();
        UpdateDeleteNumText();
    }

    public bool TryQuickEquipInventoryItem(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || GameMgr.Package == null)
        {
            return false;
        }

        Item config = GameMgr.Package.GetItemConfig(inventoryItem.itemId);
        if (config is not WeaponItem weaponItem)
        {
            return false;
        }

        EquipPanel equipPanel = GameMgr.UI.GetPanelWithoutLoad<EquipPanel>();
        if (equipPanel != null && equipPanel.gameObject.activeInHierarchy)
        {
            return equipPanel.TryEquipInventoryItem(inventoryItem);
        }

        string[] compatibleSlotKeys = GetCompatibleSlotKeys(weaponItem.equipSlot);
        if (compatibleSlotKeys == null || compatibleSlotKeys.Length == 0)
        {
            return false;
        }

        if (weaponItem.equipSlot == EquipmentSlot.Weapon && AreAllSlotsEquipped(compatibleSlotKeys))
        {
            ShowWeaponReplaceTip(inventoryItem);
            return false;
        }

        for (int i = 0; i < compatibleSlotKeys.Length; i++)
        {
            string slotKey = compatibleSlotKeys[i];
            if (GameMgr.Package.GetEquippedInventoryItem(slotKey) != null)
            {
                continue;
            }

            EquipInventoryItemToSlot(slotKey, inventoryItem);
            return true;
        }

        EquipInventoryItemToSlot(compatibleSlotKeys[0], inventoryItem);
        return true;
    }

    public void SelectTab(ItemType type)
    {
        currentType = type;
        UpdateTabHighlight(currentType);
        RefreshUi();
    }

    public void OnTabClicked(PackageTabItemUI tabItem)
    {
        if (tabItem == null)
        {
            return;
        }

        SelectTab(tabItem.ItemType);
    }

    public bool ResolveDropTargets(
        PointerEventData eventData,
        PackageItemUI sourceUI,
        out PackageItemUI targetUI,
        out EquipSlotUI equipSlotUI,
        out PlayerMainLiquidSlotUI liquidSlotUI)
    {
        targetUI = null;
        equipSlotUI = null;
        liquidSlotUI = null;

        if (EventSystem.current == null || eventData == null)
        {
            return false;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            PackageItemUI hitPackageItem = hitObject.GetComponentInParent<PackageItemUI>();
            if (hitPackageItem != null && hitPackageItem != sourceUI)
            {
                targetUI = hitPackageItem;
                return true;
            }

            EquipSlotUI hitEquipSlot = hitObject.GetComponentInParent<EquipSlotUI>();
            if (hitEquipSlot != null)
            {
                equipSlotUI = hitEquipSlot;
                return true;
            }

            PlayerMainLiquidSlotUI hitLiquidSlot = hitObject.GetComponentInParent<PlayerMainLiquidSlotUI>();
            if (hitLiquidSlot != null)
            {
                liquidSlotUI = hitLiquidSlot;
                return true;
            }
        }

        return false;
    }

    public void RefreshPackageSlotVisual(PackageItemUI sourceUI)
    {
        if (sourceUI == null)
        {
            return;
        }

        sourceUI.SetDraggingState(false);
    }

    public void BindReferences()
    {
        weaponTab = transform.Find("CenterTop/BK/Menus/Weapon");
        consumTab = transform.Find("CenterTop/BK/Menus/Consum");
        materialTab = transform.Find("CenterTop/BK/Menus/Material");
        closeButton = transform.Find("RightTop/CloseButton");
        sortButton = transform.Find("RightBot/SortButton");
        deleteButton = transform.Find("RightBot/DeleteButton");
        coinNum = transform.Find("LeftBot/CoinNum");
        scrollView = transform.Find("Center/Scroll View");
        deletePanel = transform.Find("DeletePanel");
        backButton = transform.Find("DeletePanel/BackButton");
        ensureButton = transform.Find("DeletePanel/EnsureButton");
        deleteNum = transform.Find("DeletePanel/DeleteNum");

        if (deletePanel != null)
        {
            deletePanel.gameObject.SetActive(false);
        }

        if (coinNum != null)
        {
            coinNumText = coinNum.GetComponent<TMP_Text>();
            coinNumLegacyText = coinNum.GetComponent<Text>();
        }
    }

    private void BindClicks()
    {
        BindButton(closeButton, OnClickClose);
        BindButton(sortButton, OnClickSort);
        BindButton(deleteButton, OnClickDelete);
        BindButton(backButton, OnClickBack);
        BindButton(ensureButton, OnClickEnsure);

        weaponTabItem = SetupTabInteraction(weaponTab, ItemType.Weapon);
        consumTabItem = SetupTabInteraction(consumTab, ItemType.Consumable);
        materialTabItem = SetupTabInteraction(materialTab, ItemType.Material);
    }

    private static void BindButton(Transform target, UnityEngine.Events.UnityAction callback)
    {
        if (target == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }

    private PackageTabItemUI SetupTabInteraction(Transform tabRoot, ItemType type)
    {
        if (tabRoot == null)
        {
            return null;
        }

        PackageTabItemUI tabItem = tabRoot.GetComponent<PackageTabItemUI>();
        if (tabItem == null)
        {
            tabItem = tabRoot.gameObject.AddComponent<PackageTabItemUI>();
        }

        tabItem.Setup(this, type);
        return tabItem;
    }

    private void EnsureTabItems()
    {
        if (weaponTabItem == null && weaponTab != null)
        {
            weaponTabItem = weaponTab.GetComponent<PackageTabItemUI>();
        }

        if (consumTabItem == null && consumTab != null)
        {
            consumTabItem = consumTab.GetComponent<PackageTabItemUI>();
        }

        if (materialTabItem == null && materialTab != null)
        {
            materialTabItem = materialTab.GetComponent<PackageTabItemUI>();
        }
    }

    private void RefreshCurrentTab()
    {
        EnsureTabItems();
        if (!Enum.IsDefined(typeof(ItemType), currentType))
        {
            currentType = ItemType.Weapon;
        }

        UpdateTabHighlight(currentType);
        RefreshUi();
    }

    private void UpdateTabHighlight(ItemType selectedType)
    {
        EnsureTabItems();
        weaponTabItem?.SetSelected(selectedType == ItemType.Weapon);
        consumTabItem?.SetSelected(selectedType == ItemType.Consumable);
        materialTabItem?.SetSelected(selectedType == ItemType.Material);
    }

    private void OnClickSort()
    {
        GameMgr.Package.SortItemsByType(currentType);
        RefreshUi();
    }

    private void OnClickDelete()
    {
        isInDeleteMode = true;
        selectedDeleteItems.Clear();

        if (deletePanel != null)
        {
            deletePanel.gameObject.SetActive(true);
        }

        UpdateDeleteNumText();
    }

    private void OnClickBack()
    {
        isInDeleteMode = false;
        selectedDeleteItems.Clear();

        if (deletePanel != null)
        {
            deletePanel.gameObject.SetActive(false);
        }

        RefreshUi();
    }

    private async void OnClickEnsure()
    {
        if (selectedDeleteItems.Count == 0)
        {
            return;
        }

        TipPanel tipPanel = await GameMgr.UI.GetPanel<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip("是否要删除已选择的物品？", () =>
        {
            for (int i = 0; i < selectedDeleteItems.Count; i++)
            {
                InventoryItem item = selectedDeleteItems[i];
                GameMgr.Package.RemoveItem(item.uid, item.count);
            }

            OnClickBack();
        }, null);
    }

    private void OnClickClose()
    {
        GameMgr.UI.HidePanel<PackagePanel>();
    }

    private void UpdateDeleteNumText()
    {
        if (deleteNum == null)
        {
            return;
        }

        string text = $"已选 {selectedDeleteItems.Count}/100";
        TMP_Text tmp = deleteNum.GetComponent<TMP_Text>();
        Text legacy = deleteNum.GetComponent<Text>();

        if (tmp != null)
        {
            tmp.text = text;
        }

        if (legacy != null)
        {
            legacy.text = text;
        }
    }

    private void RefreshCoinNum()
    {
        int gold = GameMgr.Package != null ? GameMgr.Package.GetGold() : 0;
        string text = gold.ToString();

        if (coinNumText != null)
        {
            coinNumText.text = text;
        }

        if (coinNumLegacyText != null)
        {
            coinNumLegacyText.text = text;
        }
    }

    private void EquipInventoryItemToSlot(string slotKey, InventoryItem inventoryItem)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || inventoryItem == null || GameMgr.Package == null)
        {
            return;
        }

        GameMgr.Package.SetEquippedItem(slotKey, inventoryItem.uid);
        RefreshUi();
        RefreshEquipPanel();
    }

    private static string[] GetCompatibleSlotKeys(EquipmentSlot equipSlot)
    {
        switch (equipSlot)
        {
            case EquipmentSlot.Head:
                return new[] { HeadSlotKey };
            case EquipmentSlot.Chest:
                return new[] { ChestSlotKey };
            case EquipmentSlot.Leg:
                return new[] { LegSlotKey };
            case EquipmentSlot.Weapon:
                return new[] { WeaponSlot1Key, WeaponSlot2Key };
            case EquipmentSlot.Tool:
                return new[] { ToolSlot1Key, ToolSlot2Key };
            case EquipmentSlot.Accessory:
                return new[] { AccessorySlot1Key, AccessorySlot2Key, AccessorySlot3Key };
            default:
                return null;
        }
    }

    private static bool AreAllSlotsEquipped(string[] slotKeys)
    {
        if (slotKeys == null || slotKeys.Length == 0 || GameMgr.Package == null)
        {
            return false;
        }

        for (int i = 0; i < slotKeys.Length; i++)
        {
            if (GameMgr.Package.GetEquippedInventoryItem(slotKeys[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private async void ShowWeaponReplaceTip(InventoryItem inventoryItem)
    {
        EquipTipPanel tipPanel = await GameMgr.UI.ShowPanel<EquipTipPanel>();
        if (tipPanel == null)
        {
            Debug.LogError("[PackagePanel] Failed to show EquipTipPanel.");
            return;
        }

        tipPanel.ShowForWeaponReplacement(null, inventoryItem);
    }

    private static void RefreshEquipPanel()
    {
        EquipPanel equipPanel = GameMgr.UI.GetPanelWithoutLoad<EquipPanel>();
        if (equipPanel != null && equipPanel.gameObject.activeInHierarchy)
        {
            equipPanel.RefreshEquipSlots();
        }
    }

    private async void RefreshScroll()
    {
        int requestVersion = ++refreshVersion;
        if (scrollView == null)
        {
            return;
        }

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        if (scrollRect == null || scrollRect.content == null)
        {
            return;
        }

        RectTransform scrollContent = scrollRect.content;
        itemUIByUid.Clear();

        GameObject prefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("PackageItemUI");
        if (requestVersion != refreshVersion || prefab == null)
        {
            return;
        }

        List<InventoryItem> items = await GameMgr.Package.GetItemsByType(currentType);
        if (requestVersion != refreshVersion)
        {
            return;
        }

        Dictionary<int, InventoryItem> itemBySlot = new Dictionary<int, InventoryItem>();
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (item == null || GameMgr.Package.IsEquipped(item.uid))
            {
                continue;
            }

            itemBySlot[item.slotIndex] = item;
        }

        int highestSlot = -1;
        foreach (KeyValuePair<int, InventoryItem> pair in itemBySlot)
        {
            if (pair.Key > highestSlot)
            {
                highestSlot = pair.Key;
            }
        }

        int totalSlots = Mathf.Max(MinSlots, highestSlot + 1);
        if (totalSlots % RowSize != 0)
        {
            totalSlots += RowSize - (totalSlots % RowSize);
        }

        for (int i = 0; i < totalSlots; i++)
        {
            PackageItemUI ui = GetOrCreateItemUI(i, prefab, scrollContent);
            ui.gameObject.SetActive(true);
            itemBySlot.TryGetValue(i, out InventoryItem inventoryItem);
            ui.Refresh(inventoryItem, this, i);

            if (inventoryItem != null)
            {
                itemUIByUid[inventoryItem.uid] = ui;
            }
        }

        for (int i = totalSlots; i < pooledItemUIs.Count; i++)
        {
            if (pooledItemUIs[i] != null)
            {
                pooledItemUIs[i].gameObject.SetActive(false);
            }
        }
    }

    private PackageItemUI GetOrCreateItemUI(int index, GameObject prefab, Transform parent)
    {
        if (index < pooledItemUIs.Count && pooledItemUIs[index] != null)
        {
            return pooledItemUIs[index];
        }

        GameObject instance = Instantiate(prefab, parent);
        PackageItemUI ui = instance.GetComponent<PackageItemUI>();
        pooledItemUIs.Add(ui);
        return ui;
    }

    public void PositionItemInfoPanel(ItemInfoPanel infoPanel, PackageItemUI itemUI)
    {
        RectTransform infoRect = infoPanel.GetComponent<RectTransform>();
        RectTransform itemRect = itemUI.GetComponent<RectTransform>();
        if (infoRect == null || itemRect == null)
        {
            return;
        }

        int index = itemUI.ItemIndex;
        int col = index % RowSize;
        int row = index / RowSize;

        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners);

        float pivotX = col < 3 ? 0f : 1f;
        float pivotY = row >= 3 ? 0f : 1f;
        infoRect.pivot = new Vector2(pivotX, pivotY);

        if (col < 3)
        {
            infoPanel.transform.position = row >= 3 ? corners[2] : corners[3];
        }
        else
        {
            infoPanel.transform.position = row >= 3 ? corners[1] : corners[0];
        }
    }
}
