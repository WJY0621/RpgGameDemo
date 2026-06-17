using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 区域生成配置。
/// 描述某一类区域内可以生成哪些怪物、生成节奏、生成范围。
/// 同一份配置可以被多个 SpawnZone 实例复用（例如多片"森林"区域共用一份配置）。
/// </summary>
[CreateAssetMenu(fileName = "SpawnZoneSO", menuName = "Game/Spawn Zone")]
public class SpawnZoneSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("区域显示名称（用于调试与 UI）")]
    public string zoneName;

    [Header("Monsters")]
    [Tooltip("该区域允许生成的怪物列表，按权重随机抽取")]
    public List<MonsterSpawnEntry> monsterEntries = new List<MonsterSpawnEntry>();

    [Header("Spawn Control")]
    [Tooltip("该区域同时存活的怪物数量上限")]
    [Min(1)] public int maxAliveCount = 5;

    [Tooltip("两次生成之间的间隔（秒）")]
    [Min(0.1f)] public float spawnInterval = 3f;

    [Header("Spawn Range (around player)")]
    [Tooltip("距玩家最近的生成距离（避免怪物在玩家脚下生成）")]
    [Min(0f)] public float minSpawnRadius = 8f;

    [Tooltip("距玩家最远的生成距离（避免生成在玩家视野之外）")]
    [Min(0f)] public float maxSpawnRadius = 15f;

    public bool HasValidEntries()
    {
        if (monsterEntries == null || monsterEntries.Count == 0)
            return false;

        for (int i = 0; i < monsterEntries.Count; i++)
        {
            if (monsterEntries[i] != null && monsterEntries[i].weight > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 按权重随机抽取一只怪物配置。
    /// </summary>
    public MonsterSpawnEntry RollRandomMonster()
    {
        if (!HasValidEntries())
            return null;

        int totalWeight = 0;
        for (int i = 0; i < monsterEntries.Count; i++)
        {
            MonsterSpawnEntry entry = monsterEntries[i];
            if (entry != null && entry.weight > 0)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < monsterEntries.Count; i++)
        {
            MonsterSpawnEntry entry = monsterEntries[i];
            if (entry == null || entry.weight <= 0)
                continue;

            roll -= entry.weight;
            if (roll < 0)
                return entry;
        }

        return null;
    }

    private void OnValidate()
    {
        if (maxSpawnRadius < minSpawnRadius)
            maxSpawnRadius = minSpawnRadius;
    }
}

[Serializable]
public class MonsterSpawnEntry
{
    [Tooltip("MonsterData.json 中的怪物 ID（必须已在 MonsterPoolConfig 中注册）")]
    public int monsterID;

    [Tooltip("权重，越大越容易被选中")]
    [Min(1)] public int weight = 1;
}
