using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 矿石专用对象池。按 resourceId 分池管理。
/// 矿石被挖光后通过 HarvestableResource.OnDepleted 通知 OreSpawnSlot，由 slot 负责调用 Return。
/// </summary>
public class OrePool : MonoBehaviour
{
    public static OrePool Instance { get; private set; }

    private class PoolEntry
    {
        public Queue<HarvestableResource> available = new Queue<HarvestableResource>();
        public OrePoolConfig.Entry config;
        public int totalCount;
    }

    [SerializeField] private OrePoolConfig config;

    [Tooltip("活跃中的矿石实例的父节点，避免场景根节点被 (Clone) 实例污染。为空时自动创建一个名为 OreModels 的子节点。")]
    [SerializeField] private Transform spawnedRoot;

    private readonly Dictionary<string, PoolEntry> pools = new Dictionary<string, PoolEntry>();
    private Transform poolRoot;

    private void Awake()
    {
        Instance = this;
        poolRoot = new GameObject("OrePool_Root").transform;
        poolRoot.SetParent(transform);

        if (spawnedRoot == null)
        {
            spawnedRoot = new GameObject("OreModels").transform;
            spawnedRoot.SetParent(transform);
        }

        // 在 Awake 阶段就初始化，保证任何 Start() 中调用 Get() 都能拿到池子
        // （否则 OreSpawnSlot.Start 可能先于 OrePool.Start 触发，导致 pools 字典为空）
        if (config != null)
            Initialize(config);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Initialize(OrePoolConfig poolConfig)
    {
        config = poolConfig;
        foreach (var entry in poolConfig.entries)
        {
            if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.resourceId))
                continue;

            PoolEntry poolEntry = GetOrCreatePoolEntry(entry);
            for (int i = 0; i < entry.initialSize; i++)
            {
                HarvestableResource ore = CreateOre(poolEntry);
                if (ore != null)
                    poolEntry.available.Enqueue(ore);
            }
        }
    }

    public HarvestableResource Get(string resourceId, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(resourceId) || !pools.TryGetValue(resourceId, out PoolEntry entry))
        {
            Debug.LogWarning($"[OrePool] 未找到 resourceId={resourceId} 的池，请检查 OrePoolConfig");
            return null;
        }

        HarvestableResource ore;
        if (entry.available.Count > 0)
        {
            ore = entry.available.Dequeue();
        }
        else if (entry.totalCount < entry.config.maxSize)
        {
            ore = CreateOre(entry);
        }
        else
        {
            Debug.LogWarning($"[OrePool] resourceId={resourceId} 的池已满（max={entry.config.maxSize}）");
            return null;
        }

        if (ore == null)
            return null;

        // 应用每种矿石各自的 Y 轴偏移（补偿模型中心点）
        Vector3 finalPos = position + Vector3.up * entry.config.spawnYOffset;

        ore.transform.SetParent(spawnedRoot);
        ore.transform.SetPositionAndRotation(finalPos, rotation);
        ore.gameObject.SetActive(true);
        return ore;
    }

    public void Return(HarvestableResource ore)
    {
        if (ore == null)
            return;

        string id = ore.ResourceId;
        if (string.IsNullOrEmpty(id) || !pools.TryGetValue(id, out PoolEntry entry))
        {
            Debug.LogWarning($"[OrePool] 归还了未注册的矿石 resourceId={id}，直接销毁");
            Destroy(ore.gameObject);
            return;
        }

        if (ore.gameObject.activeSelf)
            ore.gameObject.SetActive(false);
        ore.transform.SetParent(poolRoot);
        entry.available.Enqueue(ore);
    }

    public void Clear()
    {
        foreach (var pair in pools)
        {
            while (pair.Value.available.Count > 0)
            {
                HarvestableResource ore = pair.Value.available.Dequeue();
                if (ore != null)
                    Destroy(ore.gameObject);
            }
        }
        pools.Clear();
    }

    private HarvestableResource CreateOre(PoolEntry entry)
    {
        GameObject obj = Instantiate(entry.config.prefab, poolRoot);

        HarvestableResource ore = obj.GetComponent<HarvestableResource>();
        if (ore == null)
        {
            Debug.LogError($"[OrePool] Prefab '{entry.config.prefab.name}' 缺少 HarvestableResource 组件");
            Destroy(obj);
            return null;
        }

        // 让对象池接管生命周期，避免被 Destroy
        ore.SetDestroyWhenDepleted(false);

        obj.SetActive(false);
        entry.totalCount++;
        return ore;
    }

    private PoolEntry GetOrCreatePoolEntry(OrePoolConfig.Entry entryConfig)
    {
        if (!pools.TryGetValue(entryConfig.resourceId, out PoolEntry entry))
        {
            entry = new PoolEntry { config = entryConfig };
            pools[entryConfig.resourceId] = entry;
        }
        return entry;
    }
}
