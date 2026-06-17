using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum EquipmentSlot
{
    None = 0,
    Weapon = 1,
    Chest = 2,
    Head = 3,
    Leg = 4,
    Accessory = 5,
    Tool = 6
}

[Serializable]
public enum ToolType
{
    None = 0,
    Axe = 1,
    Pickaxe = 2
}

[Serializable]
public enum AccessoryAbility
{
    None = 0,
    DoubleJump = 1,
    Fly = 2
}

[Serializable]
public class WeaponItem : Item
{
    public string modelName;
    public string equipSlotCode;
    public EquipmentSlot equipSlot;
    public ToolType toolType;
    public int attackPower;
    public int defensePower;
    public int hp;
    public float critRate;
    public float critDamage;
    public float attackSpeed;
    public float moveSpeed;

    public bool IsArtifactWeapon =>
        string.Equals(equipSlotCode, "WA", StringComparison.OrdinalIgnoreCase);

    public bool IsTool => equipSlot == EquipmentSlot.Tool || toolType != ToolType.None;
}

[Serializable]
public class AccessoryItem : WeaponItem
{
    public int[] equipBuffIDs;
    public AccessoryAbility[] abilities;
}

[Serializable]
public class ConsumableItem : Item
{
    public int recoverHp;
    public int recoverMp;
    public int[] buffIDs;
}

[Serializable]
public class MaterialItem : Item
{
    public int materialType;
    public int materialAmount;
}
