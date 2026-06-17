using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 矿石区域配置：定义该区域内允许生成的矿石种类（带权重）以及重生延迟范围。
/// 同一份配置可被多个 OreSpawnZone 实例复用（例如多片矿区共享同一份配置）。
/// </summary>
[CreateAssetMenu(fileName = "OreSpawnZoneSO", menuName = "Game/Ore Spawn Zone")]
public class OreSpawnZoneSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("HarvestResourceData 中的 resourceId（如 iron_ore / silver_ore / gold_ore）")]
        public string resourceId;

        [Tooltip("权重，越大越容易被选中")]
        [Min(1)] public int weight = 1;
    }

    [Header("Identity")]
    public string zoneName;

    [Header("Ores")]
    [Tooltip("该区域允许生成的矿石列表，按权重随机抽取")]
    public List<Entry> oreEntries = new List<Entry>();

    [Header("Respawn")]
    [Tooltip("矿石被挖光后重生的最小延迟（秒）")]
    [Min(0f)] public float minRespawnDelay = 30f;

    [Tooltip("矿石被挖光后重生的最大延迟（秒）")]
    [Min(0f)] public float maxRespawnDelay = 60f;

    public bool HasValidEntries()
    {
        if (oreEntries == null || oreEntries.Count == 0)
            return false;

        for (int i = 0; i < oreEntries.Count; i++)
        {
            Entry entry = oreEntries[i];
            if (entry != null && entry.weight > 0 && !string.IsNullOrEmpty(entry.resourceId))
                return true;
        }
        return false;
    }

    public Entry RollRandomOre()
    {
        if (!HasValidEntries())
            return null;

        int totalWeight = 0;
        for (int i = 0; i < oreEntries.Count; i++)
        {
            Entry entry = oreEntries[i];
            if (entry != null && entry.weight > 0 && !string.IsNullOrEmpty(entry.resourceId))
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < oreEntries.Count; i++)
        {
            Entry entry = oreEntries[i];
            if (entry == null || entry.weight <= 0 || string.IsNullOrEmpty(entry.resourceId))
                continue;

            roll -= entry.weight;
            if (roll < 0)
                return entry;
        }

        return null;
    }

    public float RollRespawnDelay()
    {
        float min = Mathf.Max(0f, minRespawnDelay);
        float max = Mathf.Max(min, maxRespawnDelay);
        return max <= min ? min : UnityEngine.Random.Range(min, max);
    }

    private void OnValidate()
    {
        if (maxRespawnDelay < minRespawnDelay)
            maxRespawnDelay = minRespawnDelay;
    }
}
