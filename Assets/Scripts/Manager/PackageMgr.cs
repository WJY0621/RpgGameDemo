using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public enum InventoryItemLocation
{
    Inventory = 0,
    Equipped = 1
}

[Serializable]
public class InventoryItem
{
    public string uid;
    public int itemId;
    public int count;
    public bool isNew;
    public int slotIndex;
    public InventoryItemLocation location = InventoryItemLocation.Inventory;
    public string locationKey;
}

[Serializable]
public class EquippedItemData
{
    public string slotKey;
    public string uid;
}

[Serializable]
public class InventorySaveData
{
    public List<InventoryItem> items = new List<InventoryItem>();
    public List<EquippedItemData> equippedItems = new List<EquippedItemData>(); // legacy migration only
    public int gold;
}

[Serializable]
public class PlayerEquipmentStatBonus
{
    public int hp;
    public int atk;
    public int def;
    public float critRate;
    public float critDamage;
    public float moveSpeed;
    public float attackSpeed;
}

public class PackageMgr
{
    private Dictionary<int, Item> itemDict = new Dictionary<int, Item>();
    private bool isInitialized;

    private InventorySaveData saveData;
    public event Action OnInventoryChanged;

    public PackageMgr()
    {
        saveData = new InventorySaveData();
        TryLoadLegacyInventory();
    }

    public async Task Init()
    {
        if (isInitialized)
        {
            return;
        }

        EnsureInitializedSync();
        await UniTask.CompletedTask;
    }

    public void ReloadItemConfigs()
    {
        ItemJsonDatabase.Reload();
        itemDict = new Dictionary<int, Item>(ItemJsonDatabase.Items);
        isInitialized = true;
    }

    private void EnsureInitializedSync()
    {
        if (isInitialized)
        {
            return;
        }

        ItemJsonDatabase.EnsureLoaded();
        itemDict = new Dictionary<int, Item>(ItemJsonDatabase.Items);
        isInitialized = true;
    }

    private void TryLoadLegacyInventory()
    {
        string legacyPath = Path.Combine(Application.persistentDataPath, "Inventory.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                string json = File.ReadAllText(legacyPath);
                InventorySaveData legacyData = JsonUtility.FromJson<InventorySaveData>(json);
                if (legacyData != null)
                {
                    saveData = CloneSaveData(legacyData);
                    MigrateLegacyEquippedData(saveData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load legacy inventory: " + e.Message);
                saveData = new InventorySaveData();
            }
        }
    }

    public void SaveInventory()
    {
        if (GameMgr.File != null && GameMgr.File.CurrentGameFile != null)
        {
            GameMgr.File.SaveGameFile();
        }
    }

    public InventorySaveData BuildSaveData()
    {
        return CloneSaveData(saveData);
    }

    public void LoadFromSaveData(InventorySaveData incoming)
    {
        saveData = incoming != null ? CloneSaveData(incoming) : new InventorySaveData();
        MigrateLegacyEquippedData(saveData);
        NotifyInventoryChanged();
    }

    public Item GetItemConfig(int id)
    {
        EnsureInitializedSync();
        if (itemDict.TryGetValue(id, out Item item))
        {
            return item;
        }

        // 兜底：快照里没有（多为热更新增的物品、快照尚未刷新），实时回退到数据库并补进快照。
        Item live = ItemJsonDatabase.GetItem(id);
        if (live != null)
        {
            itemDict[id] = live;
        }

        return live;
    }

    public T GetItemConfig<T>(int id) where T : Item
    {
        return GetItemConfig(id) as T;
    }

    public List<InventoryItem> GetAllItems()
    {
        return saveData.items;
    }

    public async UniTask AddItem(int id, int count)
    {
        await Init();
        Item config = GetItemConfig(id);
        if (config == null)
        {
            Debug.LogWarning($"[PackageMgr] AddItem failed. Missing config for itemId: {id}");
            return;
        }

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
                    slotIndex = GetFirstEmptySlot(config.itemType),
                    location = InventoryItemLocation.Inventory,
                    locationKey = string.Empty
                };
                saveData.items.Add(newItem);
            }
        }
        else
        {
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
                    slotIndex = GetFirstEmptySlot(config.itemType),
                    location = InventoryItemLocation.Inventory,
                    locationKey = string.Empty
                };
                saveData.items.Add(newItem);
            }
        }

        SaveInventory();
        GameMgr.Message?.ShowItemObtained(config, count);
        NotifyInventoryChanged();
    }

    private int GetFirstEmptySlot(ItemType type)
    {
        var itemsInTab = saveData.items.FindAll(x =>
        {
            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type && x.location == InventoryItemLocation.Inventory;
        });

        HashSet<int> occupiedSlots = new HashSet<int>();
        foreach (var item in itemsInTab)
        {
            occupiedSlots.Add(item.slotIndex);
        }

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
        if (item == null)
        {
            return;
        }

        item.count -= count;
        if (item.count <= 0)
        {
            saveData.items.Remove(item);
        }

        SaveInventory();
        NotifyInventoryChanged();
    }

    /// <summary>
    /// 使用消耗品：扣 1 个，立即结算 recoverHp/recoverMp，并通过 BuffMgr 触发 buffID 关联的 buff。
    /// 返回 true 表示成功使用了一份。
    /// </summary>
    public bool UseConsumable(int itemId)
    {
        if (itemId <= 0)
        {
            return false;
        }

        ConsumableItem config = GetItemConfig<ConsumableItem>(itemId);
        if (config == null)
        {
            Debug.LogWarning($"[PackageMgr] UseConsumable failed. Not a consumable: id={itemId}");
            return false;
        }

        if (!HasAvailableItemCount(itemId, 1))
        {
            return false;
        }

        BossSummonArena summonArena = BossSummonArena.FindAvailableArena(itemId);
        if (summonArena != null)
        {
            if (GameMgr.Network != null && GameMgr.Network.IsSessionActive && !GameMgr.Network.IsServer)
            {
                GameMgr.Message?.RegisterMessage("Only the host can summon Bosses in multiplayer for now.", priority: MessagePriority.High);
                return false;
            }

            if (!summonArena.BeginSummon())
            {
                return false;
            }

            return RemoveAvailableItems(itemId, 1);
        }

        if (!RemoveAvailableItems(itemId, 1))
        {
            return false;
        }

        GameObject playerGO = GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.gameObject
            : null;

        if (config.recoverHp > 0 || config.recoverMp > 0)
        {
            ApplyImmediateRecovery(playerGO, config);
        }

        if (config.buffIDs != null && config.buffIDs.Length > 0 && playerGO != null && GameMgr.Buff != null)
        {
            for (int i = 0; i < config.buffIDs.Length; i++)
            {
                int bid = config.buffIDs[i];
                if (bid > 0)
                {
                    GameMgr.Buff.Apply(playerGO, bid, playerGO);
                }
            }
        }

        return true;
    }

    private static void ApplyImmediateRecovery(GameObject playerGO, ConsumableItem config)
    {
        if (playerGO == null)
        {
            return;
        }

        if (config.recoverHp > 0)
        {
            PlayerHealth health = playerGO.GetComponent<PlayerHealth>();
            health?.Heal(config.recoverHp);
        }

        if (config.recoverMp > 0)
        {
            PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
            data?.ModifyMP(config.recoverMp);
        }
    }

    public void ClearInventory()
    {
        if (saveData == null)
        {
            return;
        }

        saveData.items.Clear();
        saveData.gold = 0;
        SaveInventory();
        NotifyInventoryChanged();
        Debug.Log("[PackageMgr] Inventory cleared.");
    }

    public void AddGold(int amount)
    {
        if (saveData == null || amount <= 0)
        {
            return;
        }

        saveData.gold += amount;
        SaveInventory();
        GameMgr.Message?.ShowGoldObtained(amount);
        NotifyInventoryChanged();
    }

    public bool TrySpendGold(int amount)
    {
        if (saveData == null || amount <= 0 || saveData.gold < amount)
        {
            return false;
        }

        saveData.gold -= amount;
        SaveInventory();
        NotifyInventoryChanged();
        return true;
    }

    public int GetGold()
    {
        return saveData != null ? saveData.gold : 0;
    }

    public int GetAvailableItemCount(int itemId)
    {
        if (saveData?.items == null || itemId <= 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < saveData.items.Count; i++)
        {
            InventoryItem inventoryItem = saveData.items[i];
            if (inventoryItem == null || inventoryItem.itemId != itemId || inventoryItem.location != InventoryItemLocation.Inventory)
            {
                continue;
            }

            total += Mathf.Max(0, inventoryItem.count);
        }

        return total;
    }

    public bool HasAvailableItemCount(int itemId, int requiredCount)
    {
        if (requiredCount <= 0)
        {
            return true;
        }

        return GetAvailableItemCount(itemId) >= requiredCount;
    }

    public bool RemoveAvailableItems(int itemId, int removeCount)
    {
        if (saveData?.items == null || itemId <= 0 || removeCount <= 0)
        {
            return false;
        }

        if (GetAvailableItemCount(itemId) < removeCount)
        {
            return false;
        }

        int remaining = removeCount;
        for (int i = saveData.items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventoryItem inventoryItem = saveData.items[i];
            if (inventoryItem == null || inventoryItem.itemId != itemId || inventoryItem.location != InventoryItemLocation.Inventory)
            {
                continue;
            }

            int remove = Mathf.Min(inventoryItem.count, remaining);
            inventoryItem.count -= remove;
            remaining -= remove;

            if (inventoryItem.count <= 0)
            {
                saveData.items.RemoveAt(i);
            }
        }

        SaveInventory();
        NotifyInventoryChanged();
        return true;
    }

    public InventoryItem GetInventoryItemByUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) || saveData == null)
        {
            return null;
        }

        return saveData.items.Find(x => x.uid == uid);
    }

    public InventoryItem GetEquippedInventoryItem(string slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || saveData == null)
        {
            return null;
        }

        return saveData.items.Find(x => x.location == InventoryItemLocation.Equipped && x.locationKey == slotKey);
    }

    public Item GetEquippedItemConfig(string slotKey)
    {
        InventoryItem invItem = GetEquippedInventoryItem(slotKey);
        return invItem != null ? GetItemConfig(invItem.itemId) : null;
    }

    public WeaponItem GetActiveHandWeaponItem()
    {
        return GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;
    }

    public string GetActiveHandSlotKey()
    {
        return GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveHandSlotKey() : "WeaponContent1";
    }

    public void SetActiveHandSlot(string slotKey)
    {
        GameMgr.Equipment?.SetActiveHandSlot(slotKey);
    }

    public PlayerEquipmentStatBonus GetEquippedStatBonus()
    {
        return GameMgr.Equipment != null ? GameMgr.Equipment.GetEquipmentStatBonus() : new PlayerEquipmentStatBonus();
    }

    public void ApplyEquippedStatsToPlayerData(PlayerData targetData = null)
    {
        GameMgr.Equipment?.ApplyEquipmentStatsToPlayerData(targetData);
    }

    public bool IsEquipped(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) || saveData == null)
        {
            return false;
        }

        InventoryItem item = GetInventoryItemByUid(uid);
        return item != null && item.location == InventoryItemLocation.Equipped;
    }

    public void SetEquippedItem(string slotKey, string uid)
    {
        if (saveData == null || string.IsNullOrWhiteSpace(slotKey))
        {
            return;
        }

        InventoryItem currentlyEquipped = GetEquippedInventoryItem(slotKey);

        if (string.IsNullOrWhiteSpace(uid))
        {
            if (currentlyEquipped != null)
            {
                Item config = GetItemConfig(currentlyEquipped.itemId);
                currentlyEquipped.location = InventoryItemLocation.Inventory;
                currentlyEquipped.locationKey = string.Empty;
                currentlyEquipped.slotIndex = config != null ? GetFirstEmptySlot(config.itemType) : GetFirstEmptyInventorySlotRaw();
                SaveInventory();
                NotifyInventoryChanged();
            }

            return;
        }

        InventoryItem targetItem = GetInventoryItemByUid(uid);
        if (targetItem == null)
        {
            return;
        }

        if (currentlyEquipped != null && currentlyEquipped.uid != uid)
        {
            Item equippedConfig = GetItemConfig(currentlyEquipped.itemId);
            currentlyEquipped.location = InventoryItemLocation.Inventory;
            currentlyEquipped.locationKey = string.Empty;
            currentlyEquipped.slotIndex = equippedConfig != null ? GetFirstEmptySlot(equippedConfig.itemType) : GetFirstEmptyInventorySlotRaw();
        }

        targetItem.location = InventoryItemLocation.Equipped;
        targetItem.locationKey = slotKey;
        targetItem.slotIndex = -1;

        for (int i = 0; i < saveData.items.Count; i++)
        {
            InventoryItem item = saveData.items[i];
            if (item == null || item.uid == uid)
            {
                continue;
            }

            if (item.location == InventoryItemLocation.Equipped && item.locationKey == slotKey)
            {
                Item config = GetItemConfig(item.itemId);
                item.location = InventoryItemLocation.Inventory;
                item.locationKey = string.Empty;
                item.slotIndex = config != null ? GetFirstEmptySlot(config.itemType) : GetFirstEmptyInventorySlotRaw();
            }
        }

        SaveInventory();
        NotifyInventoryChanged();
    }

    public bool MoveEquippedItemToInventorySlot(string slotKey, int targetSlotIndex)
    {
        if (saveData == null || string.IsNullOrWhiteSpace(slotKey) || targetSlotIndex < 0)
        {
            return false;
        }

        InventoryItem equippedItem = GetEquippedInventoryItem(slotKey);
        if (equippedItem == null)
        {
            return false;
        }

        Item equippedConfig = GetItemConfig(equippedItem.itemId);
        if (equippedConfig == null)
        {
            return false;
        }

        InventoryItem targetItem = saveData.items.Find(x =>
        {
            if (x.uid == equippedItem.uid)
            {
                return false;
            }

            Item config = GetItemConfig(x.itemId);
            return config != null &&
                   config.itemType == equippedConfig.itemType &&
                   x.location == InventoryItemLocation.Inventory &&
                   x.slotIndex == targetSlotIndex;
        });

        int fallbackSlot = GetFirstEmptySlot(equippedConfig.itemType);
        if (targetItem != null)
        {
            targetItem.slotIndex = fallbackSlot == targetSlotIndex ? GetNextEmptySlot(equippedConfig.itemType, targetSlotIndex) : fallbackSlot;
        }

        equippedItem.location = InventoryItemLocation.Inventory;
        equippedItem.locationKey = string.Empty;
        equippedItem.slotIndex = targetSlotIndex;
        SaveInventory();
        NotifyInventoryChanged();
        return true;
    }

    public async UniTask<List<InventoryItem>> GetItemsByType(ItemType type)
    {
        await Init();
        var items = saveData.items.FindAll(x =>
        {
            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type && x.location == InventoryItemLocation.Inventory;
        });
        items.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        return items;
    }

    public void SwapItemSlots(ItemType type, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        InventoryItem itemFrom = saveData.items.Find(x =>
        {
            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type && x.location == InventoryItemLocation.Inventory && x.slotIndex == fromIndex;
        });

        InventoryItem itemTo = saveData.items.Find(x =>
        {
            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type && x.location == InventoryItemLocation.Inventory && x.slotIndex == toIndex;
        });

        if (itemFrom != null)
        {
            itemFrom.slotIndex = toIndex;
        }

        if (itemTo != null)
        {
            itemTo.slotIndex = fromIndex;
        }

        SaveInventory();
        NotifyInventoryChanged();
    }

    public async Task<int> GetRandomItemId()
    {
        await Init();
        if (itemDict.Count == 0)
        {
            return -1;
        }

        List<int> keys = new List<int>(itemDict.Keys);
        int randomIndex = UnityEngine.Random.Range(0, keys.Count);
        return keys[randomIndex];
    }

    public void SortItemsByType(ItemType type)
    {
        var items = saveData.items.FindAll(x =>
        {
            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type && x.location == InventoryItemLocation.Inventory;
        });

        items.Sort((a, b) =>
        {
            Item configA = GetItemConfig(a.itemId);
            Item configB = GetItemConfig(b.itemId);

            if (configA == null || configB == null)
            {
                return 0;
            }

            int qualityCompare = configB.quality.CompareTo(configA.quality);
            if (qualityCompare != 0)
            {
                return qualityCompare;
            }

            return a.itemId.CompareTo(b.itemId);
        });

        for (int i = 0; i < items.Count; i++)
        {
            items[i].slotIndex = i;
        }

        CompactInventorySlotsByType(type);

        SaveInventory();
        NotifyInventoryChanged();
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private int GetFirstEmptyInventorySlotRaw()
    {
        HashSet<int> occupiedSlots = new HashSet<int>();
        for (int i = 0; i < saveData.items.Count; i++)
        {
            InventoryItem item = saveData.items[i];
            if (item == null || item.location != InventoryItemLocation.Inventory || item.slotIndex < 0)
            {
                continue;
            }

            occupiedSlots.Add(item.slotIndex);
        }

        int slot = 0;
        while (occupiedSlots.Contains(slot))
        {
            slot++;
        }

        return slot;
    }

    private int GetNextEmptySlot(ItemType type, int avoidSlot)
    {
        int slot = 0;
        HashSet<int> occupiedSlots = new HashSet<int>();
        for (int i = 0; i < saveData.items.Count; i++)
        {
            InventoryItem item = saveData.items[i];
            if (item == null || item.location != InventoryItemLocation.Inventory)
            {
                continue;
            }

            Item config = GetItemConfig(item.itemId);
            if (config != null && config.itemType == type && item.slotIndex >= 0)
            {
                occupiedSlots.Add(item.slotIndex);
            }
        }

        while (occupiedSlots.Contains(slot) || slot == avoidSlot)
        {
            slot++;
        }

        return slot;
    }

    private void CompactInventorySlotsByType(ItemType type)
    {
        List<InventoryItem> items = saveData.items.FindAll(x =>
        {
            if (x == null || x.location != InventoryItemLocation.Inventory)
            {
                return false;
            }

            Item config = GetItemConfig(x.itemId);
            return config != null && config.itemType == type;
        });

        items.Sort((a, b) =>
        {
            int slotCompare = a.slotIndex.CompareTo(b.slotIndex);
            if (slotCompare != 0)
            {
                return slotCompare;
            }

            return string.CompareOrdinal(a.uid, b.uid);
        });

        for (int i = 0; i < items.Count; i++)
        {
            items[i].slotIndex = i;
        }
    }

    private static InventorySaveData CloneSaveData(InventorySaveData source)
    {
        InventorySaveData clone = new InventorySaveData
        {
            gold = source != null ? source.gold : 0,
            items = new List<InventoryItem>(),
            equippedItems = new List<EquippedItemData>()
        };

        if (source?.items != null)
        {
            for (int i = 0; i < source.items.Count; i++)
            {
                InventoryItem item = source.items[i];
                if (item == null)
                {
                    continue;
                }

                clone.items.Add(new InventoryItem
                {
                    uid = item.uid,
                    itemId = item.itemId,
                    count = item.count,
                    isNew = item.isNew,
                    slotIndex = item.slotIndex,
                    location = item.location,
                    locationKey = item.locationKey
                });
            }
        }

        if (source?.equippedItems != null)
        {
            for (int i = 0; i < source.equippedItems.Count; i++)
            {
                EquippedItemData equipped = source.equippedItems[i];
                if (equipped == null)
                {
                    continue;
                }

                clone.equippedItems.Add(new EquippedItemData
                {
                    slotKey = equipped.slotKey,
                    uid = equipped.uid
                });
            }
        }

        return clone;
    }

    private void MigrateLegacyEquippedData(InventorySaveData data)
    {
        if (data == null)
        {
            return;
        }

        for (int i = 0; i < data.items.Count; i++)
        {
            InventoryItem item = data.items[i];
            if (item == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.locationKey) && item.location != InventoryItemLocation.Equipped)
            {
                item.location = InventoryItemLocation.Inventory;
            }
        }

        if (data.equippedItems == null || data.equippedItems.Count == 0)
        {
            return;
        }

        for (int i = 0; i < data.equippedItems.Count; i++)
        {
            EquippedItemData equipped = data.equippedItems[i];
            if (equipped == null || string.IsNullOrWhiteSpace(equipped.uid))
            {
                continue;
            }

            InventoryItem item = data.items.Find(x => x.uid == equipped.uid);
            if (item == null)
            {
                continue;
            }

            item.location = InventoryItemLocation.Equipped;
            item.locationKey = equipped.slotKey;
            item.slotIndex = -1;
        }

        data.equippedItems.Clear();
    }

}
