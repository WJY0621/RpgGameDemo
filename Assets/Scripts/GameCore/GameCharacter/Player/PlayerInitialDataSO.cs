using UnityEngine;

using System;

[Serializable]
public class PlayerInitialInventoryItem
{
    [Min(1)]
    public int itemId;

    [Min(1)]
    public int count = 1;
}

[CreateAssetMenu(fileName = "PlayerInitialData", menuName = "Data/GameCharacter/Player/Player Initial Data")]
public class PlayerInitialDataSO : ScriptableObject
{
    [Header("Core Stats")]
    public int HP = 100;
    public int MP = 0;
    public int ATK = 10;
    public int DEF = 0;
    [Tooltip("玩家新建存档时的初始金币数")]
    public int GC = 0;

    [Header("Advanced Stats")]
    public float critRate = 0f;
    public float critDamage = 50f;
    public float moveSpeed = 20f;
    public float attackSpeed = 50f;

    [Header("Spawn")]
    public Vector3 initialPosition = new Vector3(184f, 3.3f, -1.5f);
    public Quaternion initialRotation = Quaternion.Euler(0f, -85f, 0f);

    [Header("Initial Buffs")]
    [Tooltip("玩家进入游戏即施加的 Buff ID 列表（永久 buff 配 duration<=0）")]
    public int[] initialBuffIDs = new int[] { 1004 };

    [Header("Initial Inventory")]
    [Tooltip("玩家新建角色存档时自动放入背包的物品 ID 和数量")]
    public PlayerInitialInventoryItem[] initialInventoryItems = new PlayerInitialInventoryItem[0];

    public PlayerData GetPlayerInitialData()
    {
        return new PlayerData
        {
            maxHP = HP,
            currentHP = HP,
            maxMP = MP,
            currentMP = MP,
            currentATK = ATK,
            currentDEF = DEF,
            currentCritRate = critRate,
            currentCritDamage = critDamage,
            currentMoveSpeed = moveSpeed,
            currentAttackSpeed = attackSpeed
        };
    }
}
