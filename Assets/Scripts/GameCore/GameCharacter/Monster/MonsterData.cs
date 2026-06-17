using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MonsterDropItemData
{
    public int itemID;
    [Range(0f, 100f)] public float dropChance;
    public int amount = 1;
}

[Serializable]
public class MonsterData
{
    [Header("Basic Info")]
    public int monsterID;
    public string monsterName;
    public string modelName;

    [Header("Base Stats")]
    public int HP = 30;
    public int ATK = 10;
    public int DEF = 0;
    public float moveSpeed = 3f;

    [Header("AI Ranges")]
    public float alertRange = 8f;
    public float chaseRange = 12f;
    public float attackRange = 2f;
    public float loseTargetRange = 18f;

    [Header("Drops")]
    public string goldDrop;
    public List<MonsterDropItemData> dropItems = new List<MonsterDropItemData>();

    public MonsterRuntime ToRuntime()
    {
        return new MonsterRuntime(this);
    }
}
