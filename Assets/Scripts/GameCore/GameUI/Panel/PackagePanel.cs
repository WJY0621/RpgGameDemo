using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackagePanel : BasePanel
{
    private Transform UIMenu;
    private Transform UIMenuWeapon;
    private Transform UIMenuConsum;
    private Transform UIMenuMaterial;
    private Transform UICloseButton;
    private Transform UISortButton;
    private Transform UIDeleteButton;
    private Transform UICoinNum;
    private Transform UIScrollView;
    private Transform UIDeletePanel;
    private Transform UIBackButton;
    private Transform UIEnsureButton;
    private Transform UIDeleteNum;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }
    public override void Init()
    {
        InitUIName();
        InitClick();
    }

    private ItemType currentType = ItemType.Weapon; 
    private InventoryItem currentSelectedItem;

    public void RefreshUi()
    {
        RefreshScroll();
    }

    private Dictionary<string, PackageItemUI> itemUIByUid = new Dictionary<string, PackageItemUI>();
    private List<PackageItemUI> pooledItemUIs = new List<PackageItemUI>(); // 对象池列表
    private const int MIN_SLOTS = 25; // 最少显示的格子数
    private const int ROW_SIZE = 5;   // 每行格子的数量
    
    // 记录当前拖拽中的物品和格子
    private PackageItemUI draggingUI;
    private Transform draggingIcon;

    public void OnBeginDragItem(PackageItemUI ui)
    {
        if (ui == null || ui.PackageItem == null) return;
        
        draggingUI = ui;
        
        // 1. 获取原图标组件
        Image sourceImg = ui.transform.Find("ItemImage")?.GetComponent<Image>();
        if (sourceImg == null || sourceImg.sprite == null)
        {
            Debug.LogError("[PackagePanel] Cannot find ItemImage or Sprite on dragged item!");
            return;
        }

        // 2. 克隆原图标物体
        GameObject iconObj = Instantiate(sourceImg.gameObject);
        iconObj.name = "DraggingIcon";
        
        // 3. 挂载到根 Canvas
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        iconObj.transform.SetParent(rootCanvas != null ? rootCanvas.transform : this.transform.parent, true);
        
        // 4. 彻底重置 RectTransform，防止 Anchor 导致飞出屏幕
        RectTransform rt = iconObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        
        // 5. 设置属性
        Image img = iconObj.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, 0.7f);
        
        // 6. 强制设置大小（解决 LayoutGroup 导致的 sizeDelta 为 0 的问题）
        float w = sourceImg.rectTransform.rect.width;
        float h = sourceImg.rectTransform.rect.height;
        if (w <= 0 || h <= 0) { w = 100; h = 100; } // 保底大小
        rt.sizeDelta = new Vector2(w, h);
        
        rt.position = sourceImg.transform.position;
        rt.localScale = Vector3.one; // 强制缩放为 1
        
        iconObj.transform.SetAsLastSibling(); 
        draggingIcon = iconObj.transform;
        
        // 隐藏原格子的图标
        ui.SetIconVisible(false);
    }

    public void OnDragItem(PointerEventData eventData)
    {
        if (draggingIcon != null)
        {
            // 兼容多种 Canvas 渲染模式的坐标转换
            Vector3 globalMousePos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(draggingIcon.parent as RectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
            {
                draggingIcon.position = globalMousePos;
            }
        }
    }

    public void OnEndDragItem(PackageItemUI targetUI)
    {
        if (draggingUI != null)
        {
            // 显示原格子图标（无论交换是否成功，都要恢复原位显示，刷新会处理位置）
            draggingUI.SetIconVisible(true);

            // 如果落点是一个有效的格子（包括空白格）
            if (targetUI != null && targetUI != draggingUI)
            {
                // 执行数据交换
                GameMgr.Package.SwapItemSlots(currentType, draggingUI.ItemIndex, targetUI.ItemIndex);
                // 刷新界面
                RefreshUi();
            }
        }

        // 清理临时图标
        if (draggingIcon != null)
        {
            Destroy(draggingIcon.gameObject);
            draggingIcon = null;
        }
        draggingUI = null;
    }

    private async void RefreshScroll()
    {
        RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        
        // 1. 将当前所有显示的格子回收进池子（隐藏即可）
        foreach (var ui in pooledItemUIs)
        {
            ui.gameObject.SetActive(false);
        }
        itemUIByUid.Clear();
        
        // 2. 加载预制体（如果池子为空）
        GameObject prefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("PackageItemUI");
        if (prefab == null)
        {
            Debug.LogError("[PackagePanel] FAILED to load 'PackageItemUI' prefab!");
            return;
        }

        // 3. 获取数据
        List<InventoryItem> items = await GameMgr.Package.GetItemsByType(currentType);
        
        // 计算需要的总格子数
        int totalSlots = Mathf.Max(MIN_SLOTS, items.Count > 0 ? items[items.Count - 1].slotIndex + 1 : 0);
        if (totalSlots % ROW_SIZE != 0)
        {
            totalSlots += (ROW_SIZE - (totalSlots % ROW_SIZE));
        }

        // 4. 生成/激活格子
        for (int i = 0; i < totalSlots; i++)
        {
            PackageItemUI ui;
            if (i < pooledItemUIs.Count)
            {
                ui = pooledItemUIs[i];
                ui.gameObject.SetActive(true);
            }
            else
            {
                GameObject obj = Instantiate(prefab, scrollContent);
                ui = obj.GetComponent<PackageItemUI>();
                pooledItemUIs.Add(ui);
            }
            
            if(ui != null)
            {
                // 关键修复：根据当前格子索引 i 查找是否有对应的物品
                InventoryItem invItem = items.Find(x => x.slotIndex == i);
                ui.Refresh(invItem, this, i);

                if (invItem != null)
                {
                    itemUIByUid[invItem.uid] = ui;
                }
            }
        }
    }


    private void InitUIName()
    {
        UIMenu = transform.Find("CenterTop/Menus");
        UIMenuWeapon = transform.Find("CenterTop/Menus/Weapon");
        UIMenuConsum = transform.Find("CenterTop/Menus/Consum");
        UIMenuMaterial = transform.Find("CenterTop/Menus/Material");
        UICloseButton = transform.Find("RightTop/CloseButton");
        UIDeleteButton = transform.Find("RightBot/DeleteButton");
        UICoinNum = transform.Find("LeftBot/CoinNum");
        UIScrollView = transform.Find("Center/Scroll View");
        UIDeletePanel = transform.Find("DeletePanel");
        UIBackButton = transform.Find("DeletePanel/BackButton");
        UIEnsureButton = transform.Find("DeletePanel/EnsureButton");
        UIDeleteNum = transform.Find("DeletePanel/DeleteNum");
        UISortButton = transform.Find("RightBot/SortButton");

        if (UIDeletePanel != null) UIDeletePanel.gameObject.SetActive(false);
    }

    private void InitClick()
    {
        if(UICloseButton) UICloseButton.GetComponent<Button>().onClick.AddListener(OnClickClose);
        if(UIDeleteButton) UIDeleteButton.GetComponent<Button>().onClick.AddListener(OnClickDelete);
        if(UIMenuWeapon) UIMenuWeapon.GetComponent<Button>().onClick.AddListener(OnClickWeapon);
        if(UIMenuConsum) UIMenuConsum.GetComponent<Button>().onClick.AddListener(OnClickConsum);
        if(UIMenuMaterial) UIMenuMaterial.GetComponent<Button>().onClick.AddListener(OnClickMaterial);
        if(UIBackButton) UIBackButton.GetComponent<Button>().onClick.AddListener(OnClickBack);
        if(UIEnsureButton) UIEnsureButton.GetComponent<Button>().onClick.AddListener(OnClickEnsure);
        if(UISortButton) UISortButton.GetComponent<Button>().onClick.AddListener(OnClickSort);
    }

    private void OnClickSort()
    {
        GameMgr.Package.SortItemsByType(currentType);
        RefreshUi();
    }
    
    private bool isInDeleteMode = false;
    public bool IsInDeleteMode => isInDeleteMode;
    private List<InventoryItem> selectedDeleteItems = new List<InventoryItem>();

    public void OnItemClickedInDeleteMode(PackageItemUI ui, InventoryItem item)
    {
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

    private void UpdateDeleteNumText()
    {
        if (UIDeleteNum != null)
        {
            string text = $"已选 {selectedDeleteItems.Count}/100";
            var textComp = UIDeleteNum.GetComponent<Text>();
            if (textComp != null) textComp.text = text;
            else
            {
                var tmpComp = UIDeleteNum.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmpComp != null) tmpComp.text = text;
            }
        }
    }

    private void OnClickMaterial()
    {
        currentType = ItemType.Material;
        RefreshUi();
    }

    private void OnClickConsum()
    {
        currentType = ItemType.Consumable;
        RefreshUi();
    }

    private void OnClickWeapon()
    {
        currentType = ItemType.Weapon;
        RefreshUi();
    }

    private void OnClickDelete()
    {
        isInDeleteMode = true;
        selectedDeleteItems.Clear();
        if (UIDeletePanel != null)
        {
            UIDeletePanel.gameObject.SetActive(true);
            UpdateDeleteNumText();
        }
    }

    private void OnClickBack()
    {
        isInDeleteMode = false;
        selectedDeleteItems.Clear();
        if (UIDeletePanel != null) UIDeletePanel.gameObject.SetActive(false);
        RefreshUi(); // 刷新以重置物品背景色
    }

    private async void OnClickEnsure()
    {
        if (selectedDeleteItems.Count == 0) return;

        TipPanel tip = await GameMgr.UI.GetPanel<TipPanel>();
        if (tip != null)
        {
            await tip.ShowTip("是否要删除已选择的物品", () =>
            {
                // 确认删除逻辑
                foreach (var item in selectedDeleteItems)
                {
                    GameMgr.Package.RemoveItem(item.uid, item.count);
                }
                OnClickBack(); // 退出删除模式并刷新
            }, () =>
            {
                // 点击返回，什么都不做，TipPanel 会自动关闭
            });
        }
    }

    private void OnClickClose()
    {
        GameMgr.UI.HidePanel<PackagePanel>();
    }

    public async void OnShowHoverInfo(InventoryItem item, PackageItemUI itemUI)
    {
        // 获取物品详细数据
        Item itemData = GameMgr.Package.GetItemConfig(item.itemId);
        if (itemData != null)
        {
            // 显示信息面板
            ItemInfoPanel infoPanel = await GameMgr.UI.ShowPanel<ItemInfoPanel>();
            if (infoPanel != null)
            {
                infoPanel.UpdatePanelInfo(itemData);
                
                // 设置面板位置 (ROW_SIZE = 5)
                int index = itemUI.ItemIndex;
                int col = index % ROW_SIZE;
                int row = index / ROW_SIZE; // 当前行数 (0 是第一行)
                
                // 获取总数据量来判断是否是最后两行
                List<InventoryItem> allItems = await GameMgr.Package.GetItemsByType(currentType);
                // 这里我们使用 MIN_SLOTS 或者是总格子数来判断，因为空白格也会触发
                // 但根据需求，我们判断当前格子在总布局中的位置
                // 假设总格子数是 totalSlots (来自 RefreshScroll 逻辑)
                // 简单起见，我们判断 row 是否处于最后两行 (总格子 25 个则 row 为 3, 4 时是最后两行)
                
                RectTransform infoRect = infoPanel.GetComponent<RectTransform>();
                RectTransform itemRect = itemUI.GetComponent<RectTransform>();
                
                Vector3[] corners = new Vector3[4];
                itemRect.GetWorldCorners(corners);

                // 确定水平 Pivot
                float pivotX = (col < 3) ? 0 : 1;
                // 确定垂直 Pivot：如果是最后两行 (row >= 3)，则向上弹出 (pivotY=0)
                float pivotY = (row >= 3) ? 0 : 1;

                infoRect.pivot = new Vector2(pivotX, pivotY);

                if (col < 3)
                {
                    // 前三列：左对齐
                    infoPanel.transform.position = (row >= 3) ? corners[2] : corners[3];
                }
                else
                {
                    // 后两列：右对齐
                    infoPanel.transform.position = (row >= 3) ? corners[1] : corners[0];
                }
            }
        }
    }
}
