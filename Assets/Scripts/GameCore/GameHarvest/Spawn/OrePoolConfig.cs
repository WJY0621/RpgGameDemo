using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 矿石对象池配置。
/// 每个条目对应 HarvestResourceData 中的一种矿石（按 resourceId 匹配）。
/// </summary>
[CreateAssetMenu(fileName = "OrePoolConfig", menuName = "Game/Ore Pool Config")]
public class OrePoolConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("HarvestResourceData 中的 resourceId（如 iron_ore / silver_ore / gold_ore）")]
        public string resourceId;

        [Tooltip("矿石 Prefab，必须挂有 HarvestableResource 组件")]
        public GameObject prefab;

        [Tooltip("场景加载时预热的数量")]
        [Min(0)] public int initialSize = 3;

        [Tooltip("池子允许同时存在的最大数量")]
        [Min(1)] public int maxSize = 30;

        [Tooltip("生成时在槽位 Y 轴上的额外偏移（用于补偿矿石 prefab 的中心点偏移）")]
        public float spawnYOffset = 0f;
    }

    public List<Entry> entries = new List<Entry>();
}
