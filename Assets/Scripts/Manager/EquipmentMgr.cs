using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EquipmentMgr
{
    private static readonly HashSet<string> HandSlotKeys = new HashSet<string>
    {
        "WeaponContent1",
        "WeaponContent2",
        "ToolContent1",
        "ToolContent2"
    };

    private PackageMgr packageMgr;
    private string activeHandSlotKey = "WeaponContent1";
    private string equippedStateSignature = string.Empty;
    private readonly HashSet<int> equippedAccessoryBuffIDs = new HashSet<int>();

    public event Action OnEquipmentChanged;
    public event Action OnCurrentWeaponModelChanged;

    public string CurrentWeaponModelName { get; private set; } = string.Empty;
    public string CurrentWeaponSlotKey { get; private set; } = string.Empty;
    public GameObject CurrentWeaponObject { get; private set; }

    public void Init(PackageMgr package)
    {
        if (packageMgr != null)
        {
            packageMgr.OnInventoryChanged -= HandleInventoryChanged;
        }

        packageMgr = package;

        if (packageMgr != null)
        {
            packageMgr.OnInventoryChanged -= HandleInventoryChanged;
            packageMgr.OnInventoryChanged += HandleInventoryChanged;
        }

        HandleEquipmentInventoryChanged();
    }

    public Item GetEquippedItem(string slotKey)
    {
        return packageMgr != null ? packageMgr.GetEquippedItemConfig(slotKey) : null;
    }

    public WeaponItem GetEquippedWeapon(string slotKey)
    {
        return GetEquippedItem(slotKey) as WeaponItem;
    }

    public Item GetEquippedHandItem(string slotKey)
    {
        return HandSlotKeys.Contains(slotKey) ? GetEquippedItem(slotKey) : null;
    }

    public string GetActiveHandSlotKey()
    {
        EnsureActiveHandSlotValid();
        return activeHandSlotKey;
    }

    public void SetActiveHandSlot(string slotKey)
    {
        if (packageMgr == null || string.IsNullOrWhiteSpace(slotKey) || !HandSlotKeys.Contains(slotKey))
        {
            return;
        }

        EnsureActiveHandSlotValid();
        if (string.Equals(activeHandSlotKey, slotKey, StringComparison.Ordinal))
        {
            return;
        }

        activeHandSlotKey = slotKey;
        RefreshEquipmentState(true);
    }

    public WeaponItem GetActiveWeapon()
    {
        if (packageMgr == null)
        {
            return null;
        }

        string slotKey = GetActiveHandSlotKey();
        return string.IsNullOrWhiteSpace(slotKey)
            ? null
            : packageMgr.GetEquippedItemConfig(slotKey) as WeaponItem;
    }

    public Item GetActiveHandItem()
    {
        if (packageMgr == null)
        {
            return null;
        }

        string slotKey = GetActiveHandSlotKey();
        return string.IsNullOrWhiteSpace(slotKey)
            ? null
            : packageMgr.GetEquippedItemConfig(slotKey);
    }

    public PlayerEquipmentStatBonus GetEquipmentStatBonus()
    {
        PlayerEquipmentStatBonus bonus = new PlayerEquipmentStatBonus();
        if (packageMgr == null)
        {
            return bonus;
        }

        string currentHandSlotKey = GetActiveHandSlotKey();
        List<InventoryItem> allItems = packageMgr.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            InventoryItem inventoryItem = allItems[i];
            if (inventoryItem == null || inventoryItem.location != InventoryItemLocation.Equipped)
            {
                continue;
            }

            WeaponItem weaponItem = packageMgr.GetItemConfig<WeaponItem>(inventoryItem.itemId);
            if (weaponItem == null)
            {
                continue;
            }

            if (HandSlotKeys.Contains(inventoryItem.locationKey) &&
                !string.Equals(inventoryItem.locationKey, currentHandSlotKey, StringComparison.Ordinal))
            {
                continue;
            }

            bonus.hp += weaponItem.hp;
            bonus.atk += weaponItem.attackPower;
            bonus.def += weaponItem.defensePower;
            bonus.critRate += weaponItem.critRate;
            bonus.critDamage += weaponItem.critDamage;
            bonus.moveSpeed += weaponItem.moveSpeed;
            bonus.attackSpeed += weaponItem.attackSpeed;
        }

        return bonus;
    }

    public void ApplyEquipmentStatsToPlayerData(PlayerData targetData = null)
    {
        PlayerData playerData = targetData ?? (GameMgr.Instance != null ? GameMgr.Instance.playerData : null);
        if (playerData == null)
        {
            return;
        }

        PlayerEquipmentStatBonus bonus = GetEquipmentStatBonus();
        playerData.ApplyEquipmentStats(
            bonus.hp,
            bonus.atk,
            bonus.def,
            bonus.critRate,
            bonus.critDamage,
            bonus.moveSpeed,
            bonus.attackSpeed);

        PlayerHealth playerHealth = GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.GetComponent<PlayerHealth>()
            : null;
        playerHealth?.RefreshStatsFromPlayerData();
    }

    public void SetCurrentWeaponModelInfo(string slotKey, string modelName, GameObject weaponObject)
    {
        CurrentWeaponSlotKey = slotKey ?? string.Empty;
        CurrentWeaponModelName = modelName ?? string.Empty;
        CurrentWeaponObject = weaponObject;
        OnCurrentWeaponModelChanged?.Invoke();
    }

    public void ClearCurrentWeaponModelInfo()
    {
        CurrentWeaponSlotKey = string.Empty;
        CurrentWeaponModelName = string.Empty;
        CurrentWeaponObject = null;
        OnCurrentWeaponModelChanged?.Invoke();
    }

    public void HandleEquipmentInventoryChanged()
    {
        RefreshEquipmentState(true);
    }

    private void HandleInventoryChanged()
    {
        RefreshEquipmentState(false);
    }

    private void RefreshEquipmentState(bool force)
    {
        EnsureActiveHandSlotValid();
        string signature = BuildEquippedStateSignature();
        if (!force && string.Equals(equippedStateSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        equippedStateSignature = signature;
        ApplyEquipmentStatsToPlayerData();
        SyncAccessoryBuffs();
        SyncPlayerAccessoryAbilities();
        OnEquipmentChanged?.Invoke();
    }

    private string BuildEquippedStateSignature()
    {
        if (packageMgr == null)
        {
            return activeHandSlotKey ?? string.Empty;
        }

        List<string> parts = new List<string>
        {
            activeHandSlotKey ?? string.Empty
        };

        List<InventoryItem> allItems = packageMgr.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            InventoryItem item = allItems[i];
            if (item == null || item.location != InventoryItemLocation.Equipped)
            {
                continue;
            }

            parts.Add($"{item.locationKey}|{item.uid}|{item.itemId}");
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    private void SyncAccessoryBuffs()
    {
        GameObject playerGO = GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.gameObject
            : null;

        if (playerGO == null || GameMgr.Buff == null || packageMgr == null)
        {
            equippedAccessoryBuffIDs.Clear();
            return;
        }

        HashSet<int> nextBuffIDs = new HashSet<int>();
        List<InventoryItem> allItems = packageMgr.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            InventoryItem inventoryItem = allItems[i];
            if (inventoryItem == null || inventoryItem.location != InventoryItemLocation.Equipped)
            {
                continue;
            }

            AccessoryItem accessory = packageMgr.GetItemConfig<AccessoryItem>(inventoryItem.itemId);
            if (accessory?.equipBuffIDs == null)
            {
                continue;
            }

            for (int j = 0; j < accessory.equipBuffIDs.Length; j++)
            {
                int buffID = accessory.equipBuffIDs[j];
                if (buffID > 0)
                {
                    nextBuffIDs.Add(buffID);
                }
            }
        }

        List<int> removeBuffer = new List<int>();
        foreach (int buffID in equippedAccessoryBuffIDs)
        {
            if (!nextBuffIDs.Contains(buffID))
            {
                removeBuffer.Add(buffID);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            GameMgr.Buff.Remove(playerGO, removeBuffer[i]);
            equippedAccessoryBuffIDs.Remove(removeBuffer[i]);
        }

        foreach (int buffID in nextBuffIDs)
        {
            if (!equippedAccessoryBuffIDs.Contains(buffID) || !GameMgr.Buff.HasBuff(playerGO, buffID))
            {
                GameMgr.Buff.Apply(playerGO, buffID, playerGO);
            }

            equippedAccessoryBuffIDs.Add(buffID);
        }
    }

    private void SyncPlayerAccessoryAbilities()
    {
        PlayerStateDriver player = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        if (player == null)
        {
            return;
        }

        bool hasDoubleJump = false;
        bool hasFly = false;

        if (packageMgr != null)
        {
            List<InventoryItem> allItems = packageMgr.GetAllItems();
            for (int i = 0; i < allItems.Count; i++)
            {
                InventoryItem inventoryItem = allItems[i];
                if (inventoryItem == null || inventoryItem.location != InventoryItemLocation.Equipped)
                {
                    continue;
                }

                AccessoryItem accessory = packageMgr.GetItemConfig<AccessoryItem>(inventoryItem.itemId);
                if (accessory?.abilities == null)
                {
                    continue;
                }

                for (int j = 0; j < accessory.abilities.Length; j++)
                {
                    switch (accessory.abilities[j])
                    {
                        case AccessoryAbility.DoubleJump:
                            hasDoubleJump = true;
                            break;
                        case AccessoryAbility.Fly:
                            hasFly = true;
                            break;
                    }
                }
            }
        }

        player.ctx.extraJumpCount = hasDoubleJump ? 1 : 0;
        if (!hasDoubleJump)
        {
            player.ctx.remainingAirJumps = 0;
        }

        player.ctx.flyUnlocked = hasFly;
        PlayerWingModelController wingModelController = player.GetComponent<PlayerWingModelController>();
        if (wingModelController == null)
        {
            wingModelController = player.gameObject.AddComponent<PlayerWingModelController>();
        }
        wingModelController.SetWingEquipped(hasFly);

        if (!hasFly)
        {
            player.ctx.isFlying = false;
            player.ctx.isGliding = false;
            player.ctx.currentFlightEnergy = Mathf.Max(0f, player.ctx.maxFlightEnergy);
        }
    }

    private void EnsureActiveHandSlotValid()
    {
        if (packageMgr == null)
        {
            activeHandSlotKey = "WeaponContent1";
            return;
        }

        if (!string.IsNullOrWhiteSpace(activeHandSlotKey) && packageMgr.GetEquippedInventoryItem(activeHandSlotKey) != null)
        {
            return;
        }

        foreach (string slotKey in HandSlotKeys)
        {
            if (packageMgr.GetEquippedInventoryItem(slotKey) != null)
            {
                activeHandSlotKey = slotKey;
                return;
            }
        }

        activeHandSlotKey = "WeaponContent1";
    }
}
