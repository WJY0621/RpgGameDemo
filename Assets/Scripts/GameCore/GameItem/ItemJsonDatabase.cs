using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ItemJsonDatabase
{
    private const string JsonRootRelativePath = "GameData/ItemData";
    private const string ResourcesRootPath = "GameData/ItemData";
    private const string EquipmentJsonFileName = "EquipmentData.json";
    private const string AccessoryJsonFileName = "AccessoryData.json";
    private const string ConsumableJsonFileName = "ConsumableData.json";
    private const string MaterialJsonFileName = "MaterialData.json";

    private static readonly Dictionary<int, Item> itemDataById = new Dictionary<int, Item>();
    private static bool isLoaded;

    public static IReadOnlyDictionary<int, Item> Items
    {
        get
        {
            EnsureLoaded();
            return itemDataById;
        }
    }

    public static void Reload()
    {
        isLoaded = false;
        itemDataById.Clear();
        EnsureLoaded();
    }

    public static Item GetItem(int itemId)
    {
        EnsureLoaded();
        itemDataById.TryGetValue(itemId, out Item item);
        return item;
    }

    public static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        itemDataById.Clear();

        LoadEquipmentItems();
        LoadAccessoryItems();
        LoadConsumableItems();
        LoadMaterialItems();
    }

    private static void LoadEquipmentItems()
    {
        string json = LoadJsonText(EquipmentJsonFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[ItemJsonDatabase] Equipment json not found: {EquipmentJsonFileName}");
            return;
        }

        EquipmentJsonCollection collection = JsonUtility.FromJson<EquipmentJsonCollection>(json);
        if (collection?.items == null)
        {
            Debug.LogWarning("[ItemJsonDatabase] Equipment json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.items.Count; i++)
        {
            EquipmentJsonRecord record = collection.items[i];
            if (record == null || record.itemId <= 0)
            {
                continue;
            }

            WeaponItem item = new WeaponItem
            {
                id = record.itemId,
                name = record.itemName,
                itemType = ItemType.Weapon,
                quality = ParseQuality(record.quality, record.qualityValue),
                description = record.description ?? string.Empty,
                functionDescription = record.functionDescription ?? string.Empty,
                capacity = 1,
                buyPrice = record.buyPrice,
                sellPrice = record.sellPrice,
                iconName = record.iconName ?? string.Empty,
                modelName = record.modelName ?? string.Empty,
                equipSlotCode = record.equipSlotCode,
                equipSlot = ParseEquipSlot(record.equipSlotCode, record.equipSlot),
                toolType = ParseToolType(record.toolType, record.equipSlotCode),
                attackPower = record.atk,
                defensePower = record.def,
                hp = record.hp,
                critRate = record.critRate,
                critDamage = record.critDamage,
                attackSpeed = record.attackSpeed,
                moveSpeed = record.moveSpeed
            };

            itemDataById[item.id] = item;
        }
    }

    private static void LoadAccessoryItems()
    {
        string json = LoadJsonText(AccessoryJsonFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[ItemJsonDatabase] Accessory json not found: {AccessoryJsonFileName}");
            return;
        }

        AccessoryJsonCollection collection = JsonUtility.FromJson<AccessoryJsonCollection>(json);
        if (collection?.items == null)
        {
            Debug.LogWarning("[ItemJsonDatabase] Accessory json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.items.Count; i++)
        {
            AccessoryJsonRecord record = collection.items[i];
            if (record == null || record.itemId <= 0)
            {
                continue;
            }

            AccessoryItem item = new AccessoryItem
            {
                id = record.itemId,
                name = record.itemName,
                itemType = ItemType.Weapon,
                quality = ParseQuality(record.quality, record.qualityValue),
                description = record.description ?? string.Empty,
                functionDescription = string.Empty,
                capacity = 1,
                buyPrice = record.buyPrice,
                sellPrice = record.sellPrice,
                iconName = record.iconName ?? string.Empty,
                modelName = string.Empty,
                equipSlotCode = "A",
                equipSlot = EquipmentSlot.Accessory,
                toolType = ToolType.None,
                equipBuffIDs = record.equipBuffIDs ?? ParseIntArray(record.equipBuffIDList),
                abilities = ParseAccessoryAbilities(record.abilities)
            };

            itemDataById[item.id] = item;
        }
    }

    private static void LoadConsumableItems()
    {
        string json = LoadJsonText(ConsumableJsonFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[ItemJsonDatabase] Consumable json not found: {ConsumableJsonFileName}");
            return;
        }

        ConsumableJsonCollection collection = JsonUtility.FromJson<ConsumableJsonCollection>(json);
        if (collection?.items == null)
        {
            Debug.LogWarning("[ItemJsonDatabase] Consumable json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.items.Count; i++)
        {
            ConsumableJsonRecord record = collection.items[i];
            if (record == null || record.itemId <= 0)
            {
                continue;
            }

            ConsumableItem item = new ConsumableItem
            {
                id = record.itemId,
                name = record.itemName,
                itemType = ItemType.Consumable,
                quality = ParseQuality(record.quality, record.qualityValue),
                description = record.description ?? string.Empty,
                functionDescription = record.functionDescription ?? string.Empty,
                capacity = Mathf.Max(1, record.maxStack),
                buyPrice = record.buyPrice,
                sellPrice = record.sellPrice,
                iconName = record.iconName ?? string.Empty,
                recoverHp = record.recoverHp,
                recoverMp = record.recoverMp,
                buffIDs = record.buffIDs ?? new int[0]
            };

            itemDataById[item.id] = item;
        }
    }

    private static void LoadMaterialItems()
    {
        string json = LoadJsonText(MaterialJsonFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[ItemJsonDatabase] Material json not found: {MaterialJsonFileName}");
            return;
        }

        MaterialJsonCollection collection = JsonUtility.FromJson<MaterialJsonCollection>(json);
        if (collection?.items == null)
        {
            Debug.LogWarning("[ItemJsonDatabase] Material json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.items.Count; i++)
        {
            MaterialJsonRecord record = collection.items[i];
            if (record == null || record.itemId <= 0)
            {
                continue;
            }

            MaterialItem item = new MaterialItem
            {
                id = record.itemId,
                name = record.itemName,
                itemType = ItemType.Material,
                quality = ParseQuality(record.quality, record.qualityValue),
                description = record.description ?? string.Empty,
                functionDescription = string.Empty,
                capacity = Mathf.Max(1, record.maxStack),
                buyPrice = record.buyPrice,
                sellPrice = record.sellPrice,
                iconName = record.iconName ?? string.Empty
            };

            itemDataById[item.id] = item;
        }
    }

    private static string LoadJsonText(string fileName)
    {
        string relativePath = Path.Combine(JsonRootRelativePath, fileName).Replace('/', Path.DirectorySeparatorChar);

        // 0. 热更目录优先：下载下来的最新配置，编辑器和打包后都生效。
        string hotUpdateRelative = $"{JsonRootRelativePath}/{fileName}";
        if (HotUpdatePaths.TryReadText(hotUpdateRelative, out string hotText) && !string.IsNullOrWhiteSpace(hotText))
        {
            return hotText;
        }

        // Editor/dev fallback for old project-path data; player builds should use Resources below.
        string assetsPath = Path.Combine(Application.dataPath, relativePath);
        if (File.Exists(assetsPath))
        {
            return File.ReadAllText(assetsPath);
        }

        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", relativePath);
        if (File.Exists(projectPath))
        {
            return File.ReadAllText(projectPath);
        }

        string resourcePath = $"{ResourcesRootPath}/{Path.GetFileNameWithoutExtension(fileName)}";
        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
        return textAsset != null ? textAsset.text : null;
    }

    private static ItemQuality ParseQuality(string qualityText, int qualityValue)
    {
        if (!string.IsNullOrWhiteSpace(qualityText) && Enum.TryParse(qualityText, true, out ItemQuality parsedQuality))
        {
            return parsedQuality;
        }

        switch (qualityValue)
        {
            case 2:
                return ItemQuality.Advanced;
            case 3:
                return ItemQuality.Rare;
            case 4:
                return ItemQuality.Epic;
            case 5:
                return ItemQuality.Legendary;
            case 1:
            default:
                return ItemQuality.Common;
        }
    }

    private static EquipmentSlot ParseEquipSlot(string slotCode, string slotText)
    {
        switch ((slotCode ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "W":
            case "WA":
                return EquipmentSlot.Weapon;
            case "C":
                return EquipmentSlot.Chest;
            case "H":
                return EquipmentSlot.Head;
            case "L":
                return EquipmentSlot.Leg;
            case "A":
                return EquipmentSlot.Accessory;
            case "T":
            case "TP":
            case "TA":
            case "TOOL":
                return EquipmentSlot.Tool;
        }

        if (!string.IsNullOrWhiteSpace(slotText) && Enum.TryParse(slotText, true, out EquipmentSlot parsedSlot))
        {
            return parsedSlot;
        }

        return EquipmentSlot.None;
    }

    private static ToolType ParseToolType(string toolTypeText, string slotCode)
    {
        if (!string.IsNullOrWhiteSpace(toolTypeText) && Enum.TryParse(toolTypeText, true, out ToolType parsedType))
        {
            return parsedType;
        }

        switch ((slotCode ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "TP":
                return ToolType.Pickaxe;
            case "TA":
                return ToolType.Axe;
        }

        return ToolType.None;
    }

    private static int[] ParseIntArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new int[0];
        }

        string[] parts = raw.Split(new[] { ',', ';', '|', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<int> values = new List<int>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i].Trim(), out int value) && value > 0)
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static AccessoryAbility[] ParseAccessoryAbilities(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AccessoryAbility[0];
        }

        string[] parts = raw.Split(new[] { ',', ';', '|', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<AccessoryAbility> values = new List<AccessoryAbility>();
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (Enum.TryParse(token, true, out AccessoryAbility parsed) && parsed != AccessoryAbility.None)
            {
                values.Add(parsed);
            }
        }

        return values.ToArray();
    }

    [Serializable]
    private class EquipmentJsonCollection
    {
        public List<EquipmentJsonRecord> items = new List<EquipmentJsonRecord>();
    }

    [Serializable]
    private class EquipmentJsonRecord
    {
        public int itemId;
        public string itemName;
        public string itemType;
        public string quality;
        public int qualityValue;
        public string equipSlot;
        public string equipSlotCode;
        public string toolType;
        public string description;
        public string functionDescription;
        public string iconName;
        public string modelName;
        public int buyPrice;
        public int sellPrice;
        public int atk;
        public int def;
        public int hp;
        public float critRate;
        public float critDamage;
        public float attackSpeed;
        public float moveSpeed;
    }

    [Serializable]
    private class AccessoryJsonCollection
    {
        public List<AccessoryJsonRecord> items = new List<AccessoryJsonRecord>();
    }

    [Serializable]
    private class AccessoryJsonRecord
    {
        public int itemId;
        public string itemName;
        public string itemType;
        public string quality;
        public int qualityValue;
        public string description;
        public string iconName;
        public int buyPrice;
        public int sellPrice;
        public int[] equipBuffIDs;
        public string equipBuffIDList;
        public string abilities;
    }

    [Serializable]
    private class ConsumableJsonCollection
    {
        public List<ConsumableJsonRecord> items = new List<ConsumableJsonRecord>();
    }

    [Serializable]
    private class ConsumableJsonRecord
    {
        public int itemId;
        public string itemName;
        public string itemType;
        public string quality;
        public int qualityValue;
        public string description;
        public string functionDescription;
        public string iconName;
        public int buyPrice;
        public int sellPrice;
        public int maxStack;
        public int recoverHp;
        public int recoverMp;
        public int[] buffIDs;
    }

    [Serializable]
    private class MaterialJsonCollection
    {
        public List<MaterialJsonRecord> items = new List<MaterialJsonRecord>();
    }

    [Serializable]
    private class MaterialJsonRecord
    {
        public int itemId;
        public string itemName;
        public string itemType;
        public string quality;
        public int qualityValue;
        public string description;
        public string iconName;
        public int buyPrice;
        public int sellPrice;
        public int maxStack;
    }
}
