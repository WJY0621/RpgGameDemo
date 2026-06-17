using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 怪物生成调度器。
/// 周期性地在玩家周围（环形范围内）按当前激活区域的配置生成怪物。
/// 依赖：
///   - <see cref="SpawnZoneRegistry"/>：提供当前生效的 SpawnZoneSO
///   - <see cref="MonsterPool"/>：负责怪物对象的获取与回收
///   - 场景已烘焙 NavMesh（若启用 useNavMeshSampling）
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Player")]
    [Tooltip("可选的玩家引用；为空时使用 GameMgr.Instance.Player.transform")]
    [SerializeField] private Transform playerOverride;

    [Header("Spawn Validation")]
    [Tooltip("是否使用 NavMesh 校验生成位置（场景需已烘焙 NavMesh）")]
    [SerializeField] private bool useNavMeshSampling = true;

    [Tooltip("NavMesh 采样最大偏移距离（候选点偏离可行走区域多远内可被吸附）")]
    [SerializeField] private float navMeshSampleDistance = 2f;

    [Tooltip("找不到合法生成点时的最大重试次数")]
    [SerializeField] private int maxSpawnAttempts = 6;

    [Header("Cleanup")]
    [Tooltip("启用远距离自动回收：玩家走远后将存活的远端怪物归还池中")]
    [SerializeField] private bool enableDistanceCleanup = true;

    [Tooltip("超出该距离的存活怪物会被回池")]
    [SerializeField] private float cleanupRadius = 30f;

    [Tooltip("远距离回收的检查间隔（秒）")]
    [SerializeField] private float cleanupCheckInterval = 2f;

    [Header("Behavior")]
    [Tooltip("Awake 完成后自动开始生成")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("玩家进入新区域时立即触发一次生成（无需等待 spawnInterval）")]
    [SerializeField] private bool burstOnZoneEntered = true;

    // 跟踪：spawnedFromZone 记录每只怪物属于哪个区域；aliveCount 记录每个区域当前存活数
    private readonly Dictionary<MonsterController, SpawnZoneSO> spawnedFromZone = new Dictionary<MonsterController, SpawnZoneSO>();
    private readonly Dictionary<SpawnZoneSO, int> aliveCount = new Dictionary<SpawnZoneSO, int>();

    private Coroutine spawnRoutine;
    private Coroutine cleanupRoutine;
    private bool isRunning;
    private bool burstRequested;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SpawnManager] Duplicate SpawnManager disabled to prevent multiple spawn loops.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        SpawnZoneRegistry.OnCurrentZoneChanged += HandleZoneChanged;
    }

    private void OnDisable()
    {
        StopSpawning();
        SpawnZoneRegistry.OnCurrentZoneChanged -= HandleZoneChanged;
    }

    private void Start()
    {
        if (autoStart)
            StartSpawning();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────

    public void StartSpawning()
    {
        if (isRunning)
            return;
        isRunning = true;
        spawnRoutine = StartCoroutine(SpawnLoop());
        if (enableDistanceCleanup)
            cleanupRoutine = StartCoroutine(CleanupLoop());
    }

    public void StopSpawning()
    {
        isRunning = false;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        if (cleanupRoutine != null)
        {
            StopCoroutine(cleanupRoutine);
            cleanupRoutine = null;
        }
    }

    public int GetAliveCount(SpawnZoneSO zone)
    {
        if (zone == null)
            return 0;
        return aliveCount.TryGetValue(zone, out int count) ? count : 0;
    }

    // ──────────────────────────────────────────────────
    // Spawn Loop
    // ──────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        while (isRunning)
        {
            SpawnZoneSO config = SpawnZoneRegistry.CurrentConfig;

            if (config == null || !config.HasValidEntries())
            {
                if (burstRequested)
                    burstRequested = false;

                // 玩家不在任何区域时短暂等待；若 burstRequested 被置位（玩家踏入区域），
                // 立即跳出循环以便第一只怪能尽快生成
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            int currentAlive = GetAliveCount(config);
            if (currentAlive < config.maxAliveCount)
                TrySpawnOne(config);

            // 每次生成之后必须等满 spawnInterval，避免一次性连刷两只
            burstRequested = false;

            float interval = Mathf.Max(0.1f, config.spawnInterval);
            yield return new WaitForSeconds(interval);
        }
    }

    private void TrySpawnOne(SpawnZoneSO config)
    {
        Transform player = GetPlayerTransform();
        if (player == null)
            return;

        MonsterSpawnEntry entry = config.RollRandomMonster();
        if (entry == null)
            return;

        if (!TryFindSpawnPosition(player.position, config, out Vector3 spawnPos))
            return;

        if (MonsterPool.Instance == null)
        {
            Debug.LogWarning("[SpawnManager] MonsterPool.Instance 不存在，无法生成怪物");
            return;
        }

        MonsterController monster = MonsterPool.Instance.Get(entry.monsterID, spawnPos, Quaternion.identity);
        if (monster == null)
            return;

        RegisterMonster(monster, config);
    }

    private bool TryFindSpawnPosition(Vector3 playerPos, SpawnZoneSO config, out Vector3 result)
    {
        float minR = Mathf.Max(0f, config.minSpawnRadius);
        float maxR = Mathf.Max(minR, config.maxSpawnRadius);

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minR, maxR);
            Vector3 candidate = playerPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (!useNavMeshSampling)
            {
                result = candidate;
                return true;
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = default;
        return false;
    }

    // ──────────────────────────────────────────────────
    // Tracking
    // ──────────────────────────────────────────────────

    private void RegisterMonster(MonsterController monster, SpawnZoneSO zone)
    {
        spawnedFromZone[monster] = zone;
        aliveCount.TryGetValue(zone, out int count);
        aliveCount[zone] = count + 1;

        MonsterHealth health = monster.Health;
        if (health != null)
        {
            health.OnDied -= HandleMonsterDied;
            health.OnDied += HandleMonsterDied;
        }
    }

    private void HandleMonsterDied(MonsterHealth health)
    {
        if (health == null)
            return;
        health.OnDied -= HandleMonsterDied;

        MonsterController monster = health.GetComponent<MonsterController>();
        UnregisterMonster(monster);
    }

    private void UnregisterMonster(MonsterController monster)
    {
        if (monster == null)
            return;

        if (spawnedFromZone.TryGetValue(monster, out SpawnZoneSO zone))
        {
            spawnedFromZone.Remove(monster);
            if (aliveCount.TryGetValue(zone, out int count))
                aliveCount[zone] = Mathf.Max(0, count - 1);
        }
    }

    private void HandleZoneChanged(SpawnZoneSO newZone)
    {
        if (burstOnZoneEntered && newZone != null)
            burstRequested = true;
    }

    // ──────────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────────

    private IEnumerator CleanupLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.5f, cleanupCheckInterval));
        while (isRunning)
        {
            yield return wait;
            CleanupFarMonsters();
        }
    }

    private void CleanupFarMonsters()
    {
        Transform player = GetPlayerTransform();
        if (player == null)
            return;

        float sqrCleanup = cleanupRadius * cleanupRadius;
        Vector3 playerPos = player.position;

        List<MonsterController> toReturn = null;
        List<MonsterController> toCleanupTracking = null;

        foreach (var pair in spawnedFromZone)
        {
            MonsterController monster = pair.Key;

            // 已被外部销毁的条目（如 MonsterPool.Clear）
            if (monster == null)
            {
                (toCleanupTracking ??= new List<MonsterController>()).Add(pair.Key);
                continue;
            }

            // 死亡中的怪物交给 MonsterReturnAgent 回收，不在这里处理
            if (monster.Health != null && monster.Health.IsDead)
                continue;

            float sqrDist = (monster.transform.position - playerPos).sqrMagnitude;
            if (sqrDist > sqrCleanup)
                (toReturn ??= new List<MonsterController>()).Add(monster);
        }

        if (toCleanupTracking != null)
        {
            for (int i = 0; i < toCleanupTracking.Count; i++)
                UnregisterMonster(toCleanupTracking[i]);
        }

        if (toReturn != null)
        {
            for (int i = 0; i < toReturn.Count; i++)
                ForceReturn(toReturn[i]);
        }
    }

    private void ForceReturn(MonsterController monster)
    {
        if (monster == null)
            return;

        if (monster.Health != null)
            monster.Health.OnDied -= HandleMonsterDied;

        UnregisterMonster(monster);

        if (MonsterPool.Instance != null)
            MonsterPool.Instance.Return(monster);
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private Transform GetPlayerTransform()
    {
        if (playerOverride != null)
            return playerOverride;
        if (GameMgr.Instance != null && GameMgr.Instance.Player != null)
            return GameMgr.Instance.Player.transform;
        return null;
    }
}
