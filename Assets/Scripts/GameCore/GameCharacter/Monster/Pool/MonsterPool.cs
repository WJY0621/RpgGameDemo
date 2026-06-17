using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物专用对象池。
/// 按 monsterID 分池管理，支持预热、动态扩容、上限控制。
/// 怪物死亡后由 MonsterReturnAgent 负责延迟回池，无需外部干预。
/// </summary>
public class MonsterPool : MonoBehaviour
{
    public static MonsterPool Instance { get; private set; }

    private class PoolEntry
    {
        public Queue<MonsterController> available = new Queue<MonsterController>();
        public HashSet<MonsterController> availableSet = new HashSet<MonsterController>();
        public MonsterPoolConfig.Entry config;
        public int totalCount;
    }

    [SerializeField] private MonsterPoolConfig config;

    [Tooltip("活跃中的怪物实例的父节点，避免场景根节点被 (Clone) 实例污染。为空时自动创建一个名为 Monsters 的子节点。")]
    [SerializeField] private Transform spawnedRoot;

    private readonly Dictionary<int, PoolEntry> pools = new Dictionary<int, PoolEntry>();
    private Transform poolRoot;
    private MonsterPoolConfig initializedConfig;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MonsterPool] Duplicate MonsterPool disabled to prevent multiple pools from spawning at once.", this);
            enabled = false;
            return;
        }

        Instance = this;
        poolRoot = new GameObject("MonsterPool_Root").transform;
        poolRoot.SetParent(transform);

        if (spawnedRoot == null)
        {
            spawnedRoot = new GameObject("Monsters").transform;
            spawnedRoot.SetParent(transform);
        }

        // 在 Awake 阶段就初始化，保证任何 Start() 中调用 Get() 都能拿到池子
        // （否则 SpawnManager.Start → SpawnLoop 可能先触发，导致 pools 字典为空）
        if (config != null)
            Initialize(config);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ──────────────────────────────────────────────────
    // 公开 API
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 用配置文件初始化并预热所有池子。可在 Start 之前手动调用以替换默认配置。
    /// </summary>
    public void Initialize(MonsterPoolConfig poolConfig)
    {
        if (poolConfig == null)
        {
            return;
        }

        if (initializedConfig == poolConfig && pools.Count > 0)
        {
            return;
        }

        config = poolConfig;
        initializedConfig = poolConfig;
        foreach (var entry in poolConfig.entries)
        {
            if (entry == null || entry.prefab == null)
                continue;

            PoolEntry poolEntry = GetOrCreatePoolEntry(entry);
            for (int i = 0; i < entry.initialSize; i++)
            {
                MonsterController monster = CreateMonster(poolEntry);
                EnqueueAvailable(poolEntry, monster);
            }
        }
    }

    /// <summary>
    /// 从池中取出一只怪物并放置到指定位置。
    /// 返回 null 表示池未注册或已达上限。
    /// </summary>
    public MonsterController Get(int monsterID, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(monsterID, out PoolEntry entry))
        {
            Debug.LogWarning($"[MonsterPool] 未找到 monsterID={monsterID} 的池，请检查 MonsterPoolConfig");
            return null;
        }

        MonsterController monster = DequeueAvailable(entry);
        if (monster == null && entry.totalCount < entry.config.maxSize)
        {
            monster = CreateMonster(entry);
        }
        else if (monster == null)
        {
            Debug.LogWarning($"[MonsterPool] monsterID={monsterID} 的池已满（max={entry.config.maxSize}）");
            return null;
        }

        if (monster == null)
            return null;

        ActivateMonster(monster, position, rotation);
        return monster;
    }

    /// <summary>
    /// 将怪物归还池中并重置为非激活状态。通常由 MonsterReturnAgent 自动调用，无需手动调用。
    /// </summary>
    public void Return(MonsterController monster)
    {
        if (monster == null)
            return;

        int id = monster.MonsterID;
        if (!pools.TryGetValue(id, out PoolEntry entry))
        {
            Debug.LogWarning($"[MonsterPool] 归还了未注册的怪物 monsterID={id}，直接销毁");
            Destroy(monster.gameObject);
            return;
        }

        if (entry.availableSet.Contains(monster))
            return;

        monster.gameObject.SetActive(false);
        monster.transform.SetParent(poolRoot);
        EnqueueAvailable(entry, monster);
    }

    /// <summary>
    /// 清空所有池（场景切换时调用）。
    /// </summary>
    public void Clear()
    {
        foreach (var pair in pools)
        {
            while (pair.Value.available.Count > 0)
            {
                MonsterController monster = pair.Value.available.Dequeue();
                pair.Value.availableSet.Remove(monster);
                if (monster != null)
                    Destroy(monster.gameObject);
            }
        }
        pools.Clear();
    }

    // ──────────────────────────────────────────────────
    // 内部方法
    // ──────────────────────────────────────────────────

    private MonsterController CreateMonster(PoolEntry entry)
    {
        GameObject obj = Instantiate(entry.config.prefab, poolRoot);

        MonsterController controller = obj.GetComponent<MonsterController>();
        if (controller == null)
        {
            Debug.LogError($"[MonsterPool] Prefab '{entry.config.prefab.name}' 上缺少 MonsterController 组件");
            Destroy(obj);
            return null;
        }

        // 强制把 prefab 的 monsterID 字段对齐到池子的注册 ID，
        // 避免设计师在 prefab 上忘填或填错导致 MonsterData.json 查不到数据
        controller.SetMonsterID(entry.config.monsterID);

        // 禁止自毁，改由对象池接管生命周期
        controller.SetMonsterID(entry.config.monsterID);

        MonsterHealth health = controller.Health;
        if (health != null)
            health.destroyOnDeath = false;

        // 挂载回池代理（若 Prefab 上已手动挂载则复用）
        MonsterReturnAgent agent = obj.GetComponent<MonsterReturnAgent>();
        if (agent == null)
            agent = obj.AddComponent<MonsterReturnAgent>();
        agent.Bind(this, controller, entry.config.returnDelay);

        obj.SetActive(false);
        entry.totalCount++;

        return controller;
    }

    private void ActivateMonster(MonsterController monster, Vector3 position, Quaternion rotation)
    {
        monster.transform.SetParent(spawnedRoot);
        monster.transform.SetPositionAndRotation(position, rotation);
        monster.gameObject.SetActive(true);
        monster.ResetForSpawn(position);
    }

    private PoolEntry GetOrCreatePoolEntry(MonsterPoolConfig.Entry config)
    {
        if (!pools.TryGetValue(config.monsterID, out PoolEntry entry))
        {
            entry = new PoolEntry { config = config };
            pools[config.monsterID] = entry;
        }
        return entry;
    }

    private static void EnqueueAvailable(PoolEntry entry, MonsterController monster)
    {
        if (entry == null || monster == null || entry.availableSet.Contains(monster))
            return;

        entry.availableSet.Add(monster);
        entry.available.Enqueue(monster);
    }

    private static MonsterController DequeueAvailable(PoolEntry entry)
    {
        while (entry.available.Count > 0)
        {
            MonsterController monster = entry.available.Dequeue();
            entry.availableSet.Remove(monster);
            if (monster != null && !monster.gameObject.activeInHierarchy)
                return monster;
        }

        return null;
    }
}
