using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class NightMonsterSpawner : MonoBehaviour
{
    private static readonly int[] DefaultMonsterIds = { 4, 6 };
    private static NightMonsterSpawner activeSpawner;

    [Header("Time")]
    [SerializeField] private float nightStartHour = 22f;
    [SerializeField] private float nightEndHour = 6f;
    [SerializeField] private float spawnInterval = 5f;

    [Header("Spawn")]
    [SerializeField] private int[] monsterIds = DefaultMonsterIds;
    [SerializeField] private float minSpawnRadius = 9f;
    [SerializeField] private float maxSpawnRadius = 15f;
    [SerializeField] private int maxAliveCount = 10;
    [SerializeField] private int maxSpawnAttempts = 8;
    [SerializeField] private float groundRayStartHeight = 12f;
    [SerializeField] private float groundRayDistance = 30f;
    [SerializeField] private float groundOffset = 0.05f;
    [SerializeField] private float cleanupRadius = 36f;

    [Header("Safe Zone")]
    [SerializeField] private float safeZoneRadius = 12f;

    private readonly List<MonsterController> aliveMonsters = new List<MonsterController>();
    private Coroutine spawnRoutine;

    private float SafeZoneRadiusSqr => safeZoneRadius * safeZoneRadius;

    private void Awake()
    {
        if (activeSpawner != null && activeSpawner != this)
        {
            Debug.LogWarning("[NightMonsterSpawner] Duplicate night spawner disabled to prevent stacked spawn loops.", this);
            enabled = false;
            return;
        }

        activeSpawner = this;
    }

    private void OnEnable()
    {
        if (activeSpawner != null && activeSpawner != this)
            return;

        activeSpawner = this;
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        for (int i = 0; i < aliveMonsters.Count; i++)
        {
            MonsterController monster = aliveMonsters[i];
            if (monster?.Health != null)
                monster.Health.OnDied -= HandleMonsterDied;
        }

        aliveMonsters.Clear();

        if (activeSpawner == this)
            activeSpawner = null;
    }

    private IEnumerator SpawnLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));

        while (enabled)
        {
            yield return wait;
            TickSpawn();
        }
    }

    private void TickSpawn()
    {
        CleanupTrackedMonsters();

        Transform player = GetPlayerTransform();
        if (player == null || MonsterPool.Instance == null)
            return;

        if (!IsNightTime() || IsInSafeZone(player.position))
            return;

        if (aliveMonsters.Count >= Mathf.Max(1, maxAliveCount))
            return;

        int monsterId = RollMonsterId();
        if (monsterId <= 0)
            return;

        if (!TryFindSpawnPosition(player.position, out Vector3 spawnPosition))
            return;

        MonsterController monster = MonsterPool.Instance.Get(monsterId, spawnPosition, Quaternion.identity);
        if (monster == null)
            return;

        UseStraightLineMovement(monster);
        RegisterMonster(monster);
    }

    private bool IsNightTime()
    {
        float currentHour = GameMgr.Time != null ? GameMgr.Time.CurrentTime : -1f;
        if (currentHour < 0f)
            return false;

        if (nightStartHour <= nightEndHour)
            return currentHour >= nightStartHour && currentHour < nightEndHour;

        return currentHour >= nightStartHour || currentHour < nightEndHour;
    }

    private bool TryFindSpawnPosition(Vector3 playerPosition, out Vector3 result)
    {
        float minRadius = Mathf.Max(0f, minSpawnRadius);
        float maxRadius = Mathf.Max(minRadius, maxSpawnRadius);

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude <= 0.0001f)
                circle = Vector2.right;

            float radius = Random.Range(minRadius, maxRadius);
            Vector3 candidate = playerPosition + new Vector3(circle.x, 0f, circle.y) * radius;
            candidate = ResolveGroundPosition(candidate, playerPosition.y);

            if (IsInSafeZone(candidate))
                continue;

            result = candidate;
            return true;
        }

        result = default;
        return false;
    }

    private Vector3 ResolveGroundPosition(Vector3 position, float fallbackY)
    {
        Vector3 rayOrigin = position + Vector3.up * groundRayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * groundOffset;

        position.y = fallbackY;
        return position;
    }

    private bool IsInSafeZone(Vector3 position)
    {
        if (IsNearNpc(position))
            return true;

        return IsNearBed(position);
    }

    private bool IsNearNpc(Vector3 position)
    {
        if (GameMgr.NPC == null)
            return false;

        List<NPCDataComponent> npcs = GameMgr.NPC.GetAllNPCs();
        for (int i = 0; i < npcs.Count; i++)
        {
            NPCDataComponent npc = npcs[i];
            if (npc == null || !npc.gameObject.activeInHierarchy)
                continue;

            if (FlatDistanceSqr(position, npc.transform.position) <= SafeZoneRadiusSqr)
                return true;
        }

        return false;
    }

    private bool IsNearBed(Vector3 position)
    {
        IReadOnlyList<BuildableObject> buildables = BuildableObject.ActiveObjects;
        for (int i = 0; i < buildables.Count; i++)
        {
            BuildableObject buildable = buildables[i];
            if (buildable == null || !buildable.gameObject.activeInHierarchy)
                continue;

            if (buildable.PieceType != BuildPieceType.Bed && buildable.InteractionType != BuildInteractionType.Bed)
                continue;

            if (FlatDistanceSqr(position, buildable.transform.position) <= SafeZoneRadiusSqr)
                return true;
        }

        return false;
    }

    private int RollMonsterId()
    {
        if (monsterIds == null || monsterIds.Length == 0)
            return -1;

        return monsterIds[Random.Range(0, monsterIds.Length)];
    }

    private void UseStraightLineMovement(MonsterController monster)
    {
        NavMeshAgent agent = monster.NavAgent;
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();

            agent.enabled = false;
        }
    }

    private void RegisterMonster(MonsterController monster)
    {
        if (aliveMonsters.Contains(monster))
            return;

        aliveMonsters.Add(monster);
        if (monster.Health != null)
        {
            monster.Health.OnDied -= HandleMonsterDied;
            monster.Health.OnDied += HandleMonsterDied;
        }
    }

    private void HandleMonsterDied(MonsterHealth health)
    {
        if (health == null)
            return;

        health.OnDied -= HandleMonsterDied;
        MonsterController monster = health.GetComponent<MonsterController>();
        aliveMonsters.Remove(monster);
    }

    private void CleanupTrackedMonsters()
    {
        Transform player = GetPlayerTransform();
        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        float cleanupRadiusSqr = cleanupRadius * cleanupRadius;

        for (int i = aliveMonsters.Count - 1; i >= 0; i--)
        {
            MonsterController monster = aliveMonsters[i];
            if (monster == null)
            {
                aliveMonsters.RemoveAt(i);
                continue;
            }

            if (monster.Health != null && monster.Health.IsDead)
            {
                aliveMonsters.RemoveAt(i);
                continue;
            }

            if (player != null && FlatDistanceSqr(playerPosition, monster.transform.position) > cleanupRadiusSqr)
            {
                if (monster.Health != null)
                    monster.Health.OnDied -= HandleMonsterDied;

                aliveMonsters.RemoveAt(i);
                MonsterPool.Instance?.Return(monster);
            }
        }
    }

    private static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }

    private static Transform GetPlayerTransform()
    {
        if (GameMgr.Instance != null && GameMgr.Instance.Player != null)
            return GameMgr.Instance.Player.transform;

        return null;
    }
}
