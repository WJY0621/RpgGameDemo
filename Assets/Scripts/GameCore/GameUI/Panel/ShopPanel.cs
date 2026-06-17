using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopPanel : BasePanel
{
    private ShopMgr shopMgr;

    private Transform itemContentRoot;
    private Transform itemTemplate;
    private Transform closeButton;
    private Transform detailIcon;
    private Transform nameText;
    private Transform infoText;
    private Transform goldText;
    private Transform needGoldText;
    private Transform remainGoldText;
    private Transform numField;
    private Transform downButton;
    private Transform upButton;
    private Transform buyButton;
    private Transform dragArea;

    private TMP_InputField tmpNumInputField;
    private InputField legacyNumInputField;
    private TMP_Text tmpNumText;
    private Text legacyNumText;
    private RectTransform panelRect;
    private RectTransform dragParentRect;
    private Vector2 dragOffset;

    private readonly List<ShopItemSlotUI> spawnedItems = new List<ShopItemSlotUI>();
    private ShopItemData selectedShopItem;
    private int selectedBuyCount = 1;
    private int refreshVersion;
    private bool isInitialized;

    public ShopMgr Shop => shopMgr;

    public override void Init()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        BindReferences();
        BindClicks();
        ClearDetail();
    }

    public override void Show()
    {
        base.Show();
        Init();
    }

    private void OnDestroy()
    {
        if (shopMgr != null)
        {
            shopMgr.OnShopChanged -= HandleShopChanged;
        }

        UnbindPackageEvents();
    }

    private void OnDisable()
    {
        UnbindPackageEvents();
    }

    public void Open(ShopMgr manager)
    {
        if (shopMgr != null)
        {
            shopMgr.OnShopChanged -= HandleShopChanged;
        }

        shopMgr = manager;

        if (shopMgr != null)
        {
            shopMgr.OnShopChanged -= HandleShopChanged;
            shopMgr.OnShopChanged += HandleShopChanged;
        }

        selectedBuyCount = 1;
        selectedShopItem = null;
        Show();
        BindPackageEvents();
        RefreshRemainGoldText();
        RefreshShopItems();
        SelectFirstAvailableItem();
    }

    public void Close()
    {
        shopMgr?.CloseShop();
    }

    public void SelectItem(ShopItemData shopItem)
    {
        if (shopItem == null)
        {
            return;
        }

        selectedShopItem = shopItem;
        selectedBuyCount = 1;
        SyncNumberField();
        RefreshSelectionState();
        RefreshDetail();
    }

    public void PreviewHoverItem(ShopItemData shopItem)
    {
        SetHoverState(shopItem, true);
    }

    public void ClearHoverItem(ShopItemData shopItem)
    {
        SetHoverState(shopItem, false);
    }

    private void BindReferences()
    {
        Transform content = transform.Find("Center/Scroll View/Viewport/Content");
        itemContentRoot = content;
        itemTemplate = content != null ? content.Find("ComItem") : null;

        closeButton = transform.Find("RightTop/CloseButton");
        dragArea = transform.Find("DragArea");
        panelRect = transform as RectTransform;
        Transform infoRoot = transform.Find("ComInfoContent");
        if (infoRoot != null)
        {
            nameText = infoRoot.Find("ComNameText");
            detailIcon = infoRoot.Find("Image");
            infoText = infoRoot.Find("ComInfoText");
            goldText = infoRoot.Find("ComGoldText");
            needGoldText = infoRoot.Find("NeedGoldText");
            remainGoldText = infoRoot.Find("RemainGoldText");
            buyButton = infoRoot.Find("BuyButton");

            Transform numRoot = infoRoot.Find("NumContent");
            if (numRoot != null)
            {
                downButton = numRoot.Find("DownButton");
                upButton = numRoot.Find("UpButton");
                numField = numRoot.Find("NumField");
            }
        }

        BindNumberFieldReferences();

        if (itemTemplate != null)
        {
            itemTemplate.gameObject.SetActive(false);
        }
    }

    private void BindNumberFieldReferences()
    {
        if (numField == null)
        {
            return;
        }

        tmpNumInputField = numField.GetComponent<TMP_InputField>();
        legacyNumInputField = numField.GetComponent<InputField>();
        tmpNumText = numField.GetComponent<TMP_Text>();
        legacyNumText = numField.GetComponent<Text>();

        if (tmpNumText == null)
        {
            Transform textTransform = FindDeepChild(numField, "Text");
            if (textTransform != null)
            {
                tmpNumText = textTransform.GetComponent<TMP_Text>();
                legacyNumText = textTransform.GetComponent<Text>();
            }
        }
    }

    private void BindClicks()
    {
        BindButton(closeButton, Close);
        BindButton(downButton, OnClickDecreaseCount);
        BindButton(upButton, OnClickIncreaseCount);
        BindButton(buyButton, OnClickBuy);
        BindDragArea();

        if (tmpNumInputField != null)
        {
            tmpNumInputField.onEndEdit.RemoveListener(OnNumberInputChanged);
            tmpNumInputField.onEndEdit.AddListener(OnNumberInputChanged);
        }

        if (legacyNumInputField != null)
        {
            legacyNumInputField.onEndEdit.RemoveListener(OnNumberInputChanged);
            legacyNumInputField.onEndEdit.AddListener(OnNumberInputChanged);
        }
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

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private void BindDragArea()
    {
        if (dragArea == null)
        {
            return;
        }

        ShopPanelDragArea dragHandler = dragArea.GetComponent<ShopPanelDragArea>();
        if (dragHandler == null)
        {
            dragHandler = dragArea.gameObject.AddComponent<ShopPanelDragArea>();
        }

        dragHandler.Setup(this);
    }

    public void BeginDragPanel(PointerEventData eventData)
    {
        if (panelRect == null)
        {
            panelRect = transform as RectTransform;
        }

        dragParentRect = panelRect != null ? panelRect.parent as RectTransform : null;
        if (panelRect == null || dragParentRect == null || eventData == null)
        {
            return;
        }

        transform.SetAsLastSibling();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragParentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            dragOffset = panelRect.anchoredPosition - localPointerPosition;
        }
    }

    public void DragPanel(PointerEventData eventData)
    {
        if (panelRect == null || dragParentRect == null || eventData == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragParentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            panelRect.anchoredPosition = localPointerPosition + dragOffset;
        }
    }

    private void RefreshShopItems()
    {
        int requestVersion = ++refreshVersion;
        ClearSpawnedItems();

        if (shopMgr?.CurrentShopData?.sellItems == null || itemTemplate == null || itemContentRoot == null)
        {
            ClearDetail();
            return;
        }

        List<ShopItemData> sellItems = shopMgr.CurrentShopData.sellItems;
        for (int i = 0; i < sellItems.Count; i++)
        {
            ShopItemData shopItem = sellItems[i];
            if (shopItem == null || shopItem.itemId <= 0)
            {
                continue;
            }

            Item item = GameMgr.Package?.GetItemConfig(shopItem.itemId);
            if (item == null)
            {
                continue;
            }

            GameObject obj = Instantiate(itemTemplate.gameObject, itemContentRoot);
            obj.SetActive(true);

            ShopItemSlotUI slotUI = obj.GetComponent<ShopItemSlotUI>();
            if (slotUI == null)
            {
                slotUI = obj.AddComponent<ShopItemSlotUI>();
            }

            slotUI.Refresh(this, shopItem, item, requestVersion);
            spawnedItems.Add(slotUI);
        }

        RefreshSelectionState();
    }

    private void ClearSpawnedItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i].gameObject);
            }
        }

        spawnedItems.Clear();
    }

    private void SelectFirstAvailableItem()
    {
        if (spawnedItems.Count == 0)
        {
            ClearDetail();
            return;
        }

        SelectItem(spawnedItems[0].ShopItem);
    }

    private void RefreshSelectionState()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ShopItemSlotUI slot = spawnedItems[i];
            if (slot == null)
            {
                continue;
            }

            slot.SetSelected(selectedShopItem != null && slot.ShopItem == selectedShopItem);
        }
    }

    private void SetHoverState(ShopItemData shopItem, bool isHover)
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            ShopItemSlotUI slot = spawnedItems[i];
            if (slot != null && slot.ShopItem == shopItem)
            {
                slot.SetHover(isHover);
                return;
            }
        }
    }

    private void RefreshDetail()
    {
        Item item = GetSelectedItemConfig();
        if (item == null)
        {
            ClearDetail();
            return;
        }

        SetText(nameText, item.name);
        SetText(infoText, item.description);
        int unitPrice = shopMgr != null ? shopMgr.GetBuyPrice(selectedShopItem) : 0;
        SetText(goldText, $"售价：{unitPrice}金/个");
        RefreshNeedGoldText();
        RefreshRemainGoldText();
        UpdateDetailIcon(item).Forget();
    }

    private void ClearDetail()
    {
        SetText(nameText, string.Empty);
        SetText(infoText, string.Empty);
        SetText(goldText, "售价：0金/个");
        SetText(needGoldText, "共需0金");
        RefreshRemainGoldText();
        SyncNumberField();

        Image icon = detailIcon != null ? detailIcon.GetComponent<Image>() : null;
        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }
    }

    private async UniTaskVoid UpdateDetailIcon(Item item)
    {
        Image icon = detailIcon != null ? detailIcon.GetComponent<Image>() : null;
        if (icon == null || item == null)
        {
            return;
        }

        Sprite sprite = await GameMgr.IconAtlas.GetItemIcon(item);
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
    }

    private void RefreshNeedGoldText()
    {
        int unitPrice = selectedShopItem != null && shopMgr != null ? shopMgr.GetBuyPrice(selectedShopItem) : 0;
        long totalPrice = (long)unitPrice * Mathf.Max(1, selectedBuyCount);
        SetText(needGoldText, $"共需{totalPrice}金");
    }

    private void RefreshRemainGoldText()
    {
        int currentGold = GameMgr.Package != null ? GameMgr.Package.GetGold() : 0;
        SetText(remainGoldText, $"剩余{currentGold}金");
    }

    private void OnClickIncreaseCount()
    {
        if (selectedShopItem == null)
        {
            return;
        }

        selectedBuyCount = Mathf.Min(GetMaxBuyCountByStock(), selectedBuyCount + 1);
        HandleBuyCountChanged();
    }

    private void OnClickDecreaseCount()
    {
        if (selectedShopItem == null)
        {
            return;
        }

        selectedBuyCount = Mathf.Max(1, selectedBuyCount - 1);
        HandleBuyCountChanged();
    }

    private void OnNumberInputChanged(string value)
    {
        if (!int.TryParse(value, out int count))
        {
            count = 1;
        }

        selectedBuyCount = Mathf.Clamp(count, 1, GetMaxBuyCountByStock());
        HandleBuyCountChanged();
    }

    private void HandleBuyCountChanged()
    {
        SyncNumberField();
        RefreshNeedGoldText();
    }

    private void SyncNumberField()
    {
        string displayValue = Mathf.Max(1, selectedBuyCount).ToString();

        if (tmpNumText != null)
        {
            tmpNumText.text = displayValue;
        }

        if (legacyNumText != null)
        {
            legacyNumText.text = displayValue;
        }

        if (tmpNumInputField != null && tmpNumInputField.text != displayValue)
        {
            tmpNumInputField.SetTextWithoutNotify(displayValue);
        }

        if (legacyNumInputField != null && legacyNumInputField.text != displayValue)
        {
            legacyNumInputField.SetTextWithoutNotify(displayValue);
        }
    }

    private async void OnClickBuy()
    {
        if (selectedShopItem == null || shopMgr == null)
        {
            return;
        }

        Item item = GetSelectedItemConfig();
        if (item == null)
        {
            return;
        }

        int count = Mathf.Clamp(selectedBuyCount, 1, GetMaxBuyCountByStock());
        int unitPrice = shopMgr.GetBuyPrice(selectedShopItem);
        long totalPrice = (long)unitPrice * count;

        TipPanel tipPanel = await GameMgr.UI.GetPanel<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        if (!shopMgr.CanBuy(selectedShopItem.itemId, count))
        {
            int stock = shopMgr.GetRemainingStock(selectedShopItem.itemId);
            int currentGold = GameMgr.Package != null ? GameMgr.Package.GetGold() : 0;
            string reason = stock >= 0 && stock < count
                ? "库存不足"
                : totalPrice > currentGold
                    ? "金币不足，无法购买"
                    : "无法购买该物品";
            await tipPanel.ShowTip(reason, null, null);
            return;
        }

        string tipText = $"是否花费{totalPrice}金购买{item.name}x{count}？";
        await tipPanel.ShowTip(tipText, () =>
        {
            BuySelectedItemAsync(selectedShopItem.itemId, count).Forget();
        }, null);
    }

    private async UniTaskVoid BuySelectedItemAsync(int itemId, int count)
    {
        if (shopMgr == null)
        {
            return;
        }

        bool success = await shopMgr.BuyItem(itemId, count);
        if (!success)
        {
            return;
        }

        selectedBuyCount = Mathf.Clamp(selectedBuyCount, 1, GetMaxBuyCountByStock());
        SyncNumberField();
        RefreshShopItems();
        RefreshDetail();
        RefreshRemainGoldText();
    }

    private void HandleShopChanged()
    {
        RefreshShopItems();
        RefreshDetail();
        RefreshRemainGoldText();
    }

    private void HandleInventoryChanged()
    {
        RefreshRemainGoldText();
    }

    private int GetMaxBuyCountByStock()
    {
        if (selectedShopItem == null || shopMgr == null)
        {
            return int.MaxValue;
        }

        int stock = shopMgr.GetRemainingStock(selectedShopItem.itemId);
        return stock >= 0 ? Mathf.Max(1, stock) : int.MaxValue;
    }

    private Item GetSelectedItemConfig()
    {
        return selectedShopItem != null ? GameMgr.Package?.GetItemConfig(selectedShopItem.itemId) : null;
    }

    private void BindPackageEvents()
    {
        if (GameMgr.Package == null)
        {
            return;
        }

        GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
        GameMgr.Package.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnbindPackageEvents()
    {
        if (GameMgr.Package == null)
        {
            return;
        }

        GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
    }

    private static void SetText(Transform target, string content)
    {
        if (target == null)
        {
            return;
        }

        TMP_Text tmp = target.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = content;
            return;
        }

        Text legacy = target.GetComponent<Text>();
        if (legacy != null)
        {
            legacy.text = content;
        }
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

public class ShopItemSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private ShopPanel owner;
    private ShopItemData shopItem;
    private Item itemConfig;
    private Transform iconTransform;
    private Transform selectionTransform;
    private int refreshVersion;
    private bool isSelected;

    public ShopItemData ShopItem => shopItem;

    public void Refresh(ShopPanel panel, ShopItemData itemData, Item item, int version)
    {
        owner = panel;
        shopItem = itemData;
        itemConfig = item;
        refreshVersion = version;

        iconTransform = transform.Find("ComIcon");
        selectionTransform = transform.Find("SelectIcon");
        if (selectionTransform == null)
        {
            selectionTransform = transform.Find("Selection");
        }

        SetSelected(false);
        UpdateIcon(version).Forget();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionTransform != null)
        {
            selectionTransform.gameObject.SetActive(selected);
        }
    }

    public void SetHover(bool isHover)
    {
        if (selectionTransform != null)
        {
            selectionTransform.gameObject.SetActive(isHover || isSelected);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.SelectItem(shopItem);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.PreviewHoverItem(shopItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.ClearHoverItem(shopItem);
    }

    private async UniTaskVoid UpdateIcon(int version)
    {
        Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (icon == null || itemConfig == null)
        {
            return;
        }

        Sprite sprite = await GameMgr.IconAtlas.GetItemIcon(itemConfig);
        if (icon != null && refreshVersion == version)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
    }

}

public class ShopPanelDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private ShopPanel owner;

    public void Setup(ShopPanel panel)
    {
        owner = panel;

        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            Image image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            graphic = image;
        }

        graphic.raycastTarget = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginDragPanel(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.DragPanel(eventData);
    }
}
