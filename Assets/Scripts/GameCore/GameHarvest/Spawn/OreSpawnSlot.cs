using System.Collections;
using UnityEngine;

/// <summary>
/// 矿石生成槽位。
/// 摆放在场景里的固定位置（建议作为 OreSpawnZone 的子节点），管理一只矿石的生命周期：
///   1. 启动时按区域配置抽一种矿石生成
///   2. 监听 HarvestableResource.OnDepleted
///   3. 矿石被挖光后归还池中，等待重生延迟后再抽一种新的矿石生成
/// </summary>
[DisallowMultipleComponent]
public class OreSpawnSlot : MonoBehaviour
{
    [Tooltip("所属区域；为空时从父级 GetComponentInParent 自动查找")]
    [SerializeField] private OreSpawnZone zone;

    [Tooltip("启动时延迟多久执行第一次生成（多个槽位可错峰生成，避免一帧瞬间实例化大量 prefab）")]
    [Min(0f)] [SerializeField] private float initialSpawnDelay;

    [Header("Gizmo")]
    [SerializeField] private bool drawGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.65f, 0.15f, 0.8f);
    [SerializeField] private float gizmoRadius = 0.35f;

    private HarvestableResource currentOre;
    private Coroutine pendingRoutine;

    private void Awake()
    {
        if (zone == null)
            zone = GetComponentInParent<OreSpawnZone>();
    }

    private void Start()
    {
        if (zone == null)
        {
            Debug.LogWarning($"[OreSpawnSlot] '{name}' 找不到 OreSpawnZone，请挂在区域父节点下或显式指定 zone", this);
            return;
        }

        ScheduleSpawn(initialSpawnDelay);
    }

    private void OnDestroy()
    {
        UnsubscribeCurrent();
    }

    public OreSpawnZone Zone => zone;
    public HarvestableResource CurrentOre => currentOre;

    // ──────────────────────────────────────────────────
    // Spawn / Respawn
    // ──────────────────────────────────────────────────

    private void ScheduleSpawn(float delay)
    {
        if (pendingRoutine != null)
            StopCoroutine(pendingRoutine);
        pendingRoutine = StartCoroutine(SpawnAfterDelay(delay));
    }

    private IEnumerator SpawnAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SpawnNewOre();
        pendingRoutine = null;
    }

    private void SpawnNewOre()
    {
        if (zone == null || zone.Config == null || !zone.Config.HasValidEntries())
            return;

        if (OrePool.Instance == null)
        {
            Debug.LogWarning("[OreSpawnSlot] OrePool.Instance 不存在，无法生成矿石");
            return;
        }

        OreSpawnZoneSO.Entry entry = zone.Config.RollRandomOre();
        if (entry == null)
            return;

        currentOre = OrePool.Instance.Get(entry.resourceId, transform.position, transform.rotation);
        if (currentOre == null)
            return;

        currentOre.OnDepleted -= HandleDepleted;
        currentOre.OnDepleted += HandleDepleted;
    }

    private void HandleDepleted(HarvestableResource resource)
    {
        if (resource != null)
        {
            resource.OnDepleted -= HandleDepleted;
            if (OrePool.Instance != null)
                OrePool.Instance.Return(resource);
        }

        currentOre = null;

        if (zone == null || zone.Config == null)
            return;

        ScheduleSpawn(zone.Config.RollRespawnDelay());
    }

    private void UnsubscribeCurrent()
    {
        if (currentOre != null)
        {
            currentOre.OnDepleted -= HandleDepleted;
            currentOre = null;
        }
    }

    // ──────────────────────────────────────────────────
    // Gizmo
    // ──────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
    }
}
