using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ShopMgr
{
    private readonly Dictionary<int, int> runtimeStockByItemId = new Dictionary<int, int>();

    private ShopDataSO currentShopData;
    private NPCDataComponent currentNPC;

    public event Action OnShopChanged;

    public ShopDataSO CurrentShopData => currentShopData;
    public NPCDataComponent CurrentNPC => currentNPC;

    public async UniTask<ShopPanel> OpenShop(NPCDataComponent npc)
    {
        currentNPC = npc;
        currentShopData = npc != null ? npc.ShopData : null;
        BuildRuntimeStock();

        if (currentShopData == null)
        {
            Debug.LogWarning($"[ShopMgr] NPC '{(npc != null ? npc.NPCName : "null")}' has no ShopDataSO assigned.");
            return null;
        }

        GameMgr.input?.EnableUIActionMap();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ShopPanel panel = await GameMgr.UI.ShowPanel<ShopPanel>();
        if (panel == null)
        {
            CloseShop();
            return null;
        }

        panel.Open(this);
        return panel;
    }

    public void CloseShop()
    {
        GameMgr.UI.HidePanel<ShopPanel>();
        GameMgr.input?.EnablePlayerActionMap();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        currentNPC = null;
        currentShopData = null;
        runtimeStockByItemId.Clear();
    }

    public int GetBuyPrice(ShopItemData shopItem)
    {
        if (shopItem == null)
        {
            return 0;
        }

        Item item = GameMgr.Package?.GetItemConfig(shopItem.itemId);
        return item != null ? Mathf.Max(0, item.buyPrice) : 0;
    }

    public int GetSellPrice(int itemId)
    {
        Item item = GameMgr.Package?.GetItemConfig(itemId);
        return item != null ? Mathf.Max(0, item.sellPrice) : 0;
    }

    public int GetRemainingStock(int itemId)
    {
        if (!runtimeStockByItemId.TryGetValue(itemId, out int stock))
        {
            return -1;
        }

        return stock;
    }

    public bool CanBuy(int itemId, int count)
    {
        if (currentShopData == null || GameMgr.Package == null || count <= 0)
        {
            return false;
        }

        ShopItemData shopItem = currentShopData.GetSellItem(itemId);
        if (shopItem == null)
        {
            return false;
        }

        int stock = GetRemainingStock(itemId);
        if (stock >= 0 && stock < count)
        {
            return false;
        }

        int price = GetBuyPrice(shopItem);
        if (price <= 0)
        {
            return false;
        }

        long totalPrice = (long)price * count;
        return totalPrice <= int.MaxValue && GameMgr.Package.GetGold() >= totalPrice;
    }

    public async UniTask<bool> BuyItem(int itemId, int count)
    {
        if (!CanBuy(itemId, count))
        {
            return false;
        }

        ShopItemData shopItem = currentShopData.GetSellItem(itemId);
        int totalPrice = GetBuyPrice(shopItem) * count;

        if (!GameMgr.Package.TrySpendGold(totalPrice))
        {
            return false;
        }

        await GameMgr.Package.AddItem(itemId, count);

        if (runtimeStockByItemId.TryGetValue(itemId, out int stock))
        {
            runtimeStockByItemId[itemId] = Mathf.Max(0, stock - count);
        }

        OnShopChanged?.Invoke();
        return true;
    }

    public bool CanSell(int itemId, int count)
    {
        if (GameMgr.Package == null || itemId <= 0 || count <= 0)
        {
            return false;
        }

        return GetSellPrice(itemId) > 0 && GameMgr.Package.HasAvailableItemCount(itemId, count);
    }

    public bool SellItem(int itemId, int count)
    {
        if (!CanSell(itemId, count))
        {
            return false;
        }

        int totalPrice = GetSellPrice(itemId) * count;
        if (!GameMgr.Package.RemoveAvailableItems(itemId, count))
        {
            return false;
        }

        GameMgr.Package.AddGold(totalPrice);
        OnShopChanged?.Invoke();
        return true;
    }

    public List<InventoryItem> GetSellableInventoryItems()
    {
        List<InventoryItem> result = new List<InventoryItem>();
        if (GameMgr.Package == null)
        {
            return result;
        }

        List<InventoryItem> allItems = GameMgr.Package.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            InventoryItem inventoryItem = allItems[i];
            if (inventoryItem == null ||
                inventoryItem.location != InventoryItemLocation.Inventory ||
                inventoryItem.count <= 0 ||
                GetSellPrice(inventoryItem.itemId) <= 0)
            {
                continue;
            }

            result.Add(inventoryItem);
        }

        return result;
    }

    private void BuildRuntimeStock()
    {
        runtimeStockByItemId.Clear();
        if (currentShopData?.sellItems == null)
        {
            return;
        }

        for (int i = 0; i < currentShopData.sellItems.Count; i++)
        {
            ShopItemData shopItem = currentShopData.sellItems[i];
            if (shopItem == null || shopItem.itemId <= 0 || shopItem.stock < 0)
            {
                continue;
            }

            runtimeStockByItemId[shopItem.itemId] = shopItem.stock;
        }
    }
}
