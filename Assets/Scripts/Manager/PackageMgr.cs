using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

// 动态数据结构
[Serializable]
public class InventoryItem
{
    public string uid;
    public int itemId;
    public int count;
    public bool isNew;
    public int slotIndex; // 记录物品所在的格子索引
}

[Serializable]
public class InventorySaveData
{
    public List<InventoryItem> items = new List<InventoryItem>();
    public int gold;
}

public class PackageMgr
{
    // 缓存所有类型的 Item
    private Dictionary<int, Item> itemDict = new Dictionary<int, Item>();
    
    // 是否已经初始化
    private bool isInitialized = false;

    private InventorySaveData saveData;
    private string savePath;

    public PackageMgr()
    {
        savePath = Path.Combine(Application.persistentDataPath, "Inventory.json");
        LoadInventory();
    }

    public async Task Init()
    {
        if (isInitialized) return;

        // 并行加载所有 Table
        var weaponTask = GameMgr.AssetLoader.LoadAsset<WeaponItemTable>("WeaponTable");
        var consumTask = GameMgr.AssetLoader.LoadAsset<ConsumableItemTable>("ConsumableTable");
        var materialTask = GameMgr.AssetLoader.LoadAsset<MaterialItemTable>("MaterialTable");

        var (weaponTable, consumTable, materialTable) = await UniTask.WhenAll(weaponTask, consumTask, materialTask);

        ProcessTable(weaponTable, "WeaponTable");
        ProcessTable(consumTable, "ConsumableTable");
        ProcessTable(materialTable, "MaterialTable");

        isInitialized = true;
    }

    private void ProcessTable<T>(T table, string tableName) where T : ScriptableObject
    {
        if (table == null)
        {
            Debug.LogError($"[PackageMgr] {tableName} FAILED to load! Check Addressables Key: '{tableName}'");
            return;
        }

        var itemsProperty = table.GetType().GetField("Items");
        if (itemsProperty == null) return;

        var items = itemsProperty.GetValue(table) as System.Collections.IEnumerable;
        if (items == null) return;

        foreach (Item item in items)
        {
            if (item == null) continue;
            
            if (item.id >= 1000 && item.id < 2000) item.itemType = ItemType.Weapon;
            else if (item.id >= 2000 && item.id < 3000) item.itemType = ItemType.Consumable;
            else if (item.id >= 3000 && item.id < 4000) item.itemType = ItemType.Material;

            if (!itemDict.ContainsKey(item.id))
            {
                itemDict.Add(item.id, item);
            }
        }
    }

    // --- 数据加载与保存 ---

    private void LoadInventory()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                saveData = JsonUtility.FromJson<InventorySaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load inventory: " + e.Message);
                saveData = new InventorySaveData();
            }
        }
        else
        {
            saveData = new InventorySaveData();
        }
    }

    public void SaveInventory()
    {
        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save inventory: " + e.Message);
        }
    }

    // --- 核心操作 API ---

    public Item GetItemConfig(int id)
    {
        if (itemDict.ContainsKey(id))
            return itemDict[id];
        return null;
    }

    // 泛型获取具体类型的配置
    public T GetItemConfig<T>(int id) where T : Item
    {
        if (itemDict.ContainsKey(id))
        {
            return itemDict[id] as T;
        }
        return null;
    }

    public List<InventoryItem> GetAllItems()
    {
        return saveData.items;
    }

    public async UniTask AddItem(int id, int count)
    {
        await Init();
        Item config = GetItemConfig(id);
        if (config == null) return;

        // 如果是武器，或者是新物品（不堆叠），则寻找新格子
        if (config.itemType == ItemType.Weapon)
        {
            for (int i = 0; i < count; i++)
            {
                InventoryItem newItem = new InventoryItem
                {
                    uid = Guid.NewGuid().ToString(),
                    itemId = id,
                    count = 1,
                    isNew = true,
                    slotIndex = GetFirstEmptySlot(config.itemType)
                };
                saveData.items.Add(newItem);
            }
        }
        else
        {
            // 尝试堆叠
            InventoryItem existingItem = saveData.items.Find(x => x.itemId == id);
            if (existingItem != null)
            {
                existingItem.count += count;
                existingItem.isNew = true;
            }
            else
            {
                InventoryItem newItem = new InventoryItem
                {
                    uid = Guid.NewGuid().ToString(),
                    itemId = id,
                    count = count,
                    isNew = true,
                    slotIndex = GetFirstEmptySlot(config.itemType)
                };
                saveData.items.Add(newItem);
            }
        }
        SaveInventory();
    }

    private int GetFirstEmptySlot(ItemType type)
    {
        var itemsInTab = saveData.items.FindAll(x => GetItemConfig(x.itemId).itemType == type);
        HashSet<int> occupiedSlots = new HashSet<int>();
        foreach (var item in itemsInTab) occupiedSlots.Add(item.slotIndex);

        int slot = 0;
        while (occupiedSlots.Contains(slot))
        {
            slot++;
        }
        return slot;
    }

    public void RemoveItem(string uid, int count)
    {
        InventoryItem item = saveData.items.Find(x => x.uid == uid);
        if (item != null)
        {
            item.count -= count;
            if (item.count <= 0)
            {
                saveData.items.Remove(item);
            }
            SaveInventory();
        }
    }

    public void ClearInventory()
    {
        if (saveData != null)
        {
            saveData.items.Clear();
            saveData.gold = 0; // 如果需要也可以清空金币
            SaveInventory();
            Debug.Log("[PackageMgr] Inventory cleared.");
        }
    }

    public async UniTask<List<InventoryItem>> GetItemsByType(ItemType type)
    {
        await Init();
        // 找出该类型的所有物品
        var items = saveData.items.FindAll(x => GetItemConfig(x.itemId).itemType == type);
        // 按 slotIndex 排序
        items.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        return items;
    }

    // 交换物品位置逻辑
    public void SwapItemSlots(ItemType type, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        // 找出该页签下，占据这两个索引的物品
        InventoryItem itemFrom = saveData.items.Find(x => GetItemConfig(x.itemId).itemType == type && x.slotIndex == fromIndex);
        InventoryItem itemTo = saveData.items.Find(x => GetItemConfig(x.itemId).itemType == type && x.slotIndex == toIndex);

        if (itemFrom != null)
        {
            itemFrom.slotIndex = toIndex;
        }

        if (itemTo != null)
        {
            itemTo.slotIndex = fromIndex;
        }

        SaveInventory();
    }
    
    // 兼容旧接口或者提供新排序
    public async Task<int> GetRandomItemId()
    {
        await Init();
        if (itemDict.Count == 0) return -1;
        
        List<int> keys = new List<int>(itemDict.Keys);
        int randomIndex = UnityEngine.Random.Range(0, keys.Count);
        return keys[randomIndex];
    }

    public void SortItemsByType(ItemType type)
    {
        var items = saveData.items.FindAll(x => GetItemConfig(x.itemId).itemType == type);
        
        // 排序逻辑：品质从高到低 (Legendary -> Common)，ID 从小到大
        items.Sort((a, b) => {
            Item configA = GetItemConfig(a.itemId);
            Item configB = GetItemConfig(b.itemId);
            
            // 先比较品质 (降序)
            int qualityCompare = configB.quality.CompareTo(configA.quality);
            if (qualityCompare != 0) return qualityCompare;
            
            // 品质相同比较 ID (升序)
            return a.itemId.CompareTo(b.itemId);
        });

        // 重新分配 slotIndex，让物品紧凑排列
        for (int i = 0; i < items.Count; i++)
        {
            items[i].slotIndex = i;
        }

        SaveInventory();
    }
}
