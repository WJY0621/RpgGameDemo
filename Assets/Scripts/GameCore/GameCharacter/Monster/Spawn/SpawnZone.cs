using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中的怪物生成区域。
/// 挂载到带 Trigger Collider 的 GameObject 上，玩家进入触发器时该区域被激活。
/// 注意：玩家根节点必须带 playerTag（默认 "Player"）。
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class SpawnZone : MonoBehaviour
{
    [SerializeField] private SpawnZoneSO config;

    [Tooltip("玩家同时位于多个区域时，优先级高的生效")]
    [SerializeField] private int priority = 0;

    [Tooltip("用于识别玩家的 Tag，会检查玩家根节点（transform.root）")]
    [SerializeField] private string playerTag = "Player";

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.15f);
    [SerializeField] private bool drawGizmoAlways = true;

    private readonly HashSet<Collider> insidePlayerColliders = new HashSet<Collider>();

    public SpawnZoneSO Config => config;
    public int Priority => priority;
    public bool IsPlayerInside { get; private set; }

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"[SpawnZone] '{name}' 的 Collider 必须是 Trigger，已自动启用 isTrigger", this);
            c.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        SpawnZoneRegistry.Register(this);
    }

    private void OnDisable()
    {
        if (IsPlayerInside)
        {
            IsPlayerInside = false;
            SpawnZoneRegistry.NotifyExited(this);
        }
        insidePlayerColliders.Clear();
        SpawnZoneRegistry.Unregister(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        insidePlayerColliders.Add(other);
        if (!IsPlayerInside)
        {
            IsPlayerInside = true;
            SpawnZoneRegistry.NotifyEntered(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        insidePlayerColliders.Remove(other);
        if (IsPlayerInside && insidePlayerColliders.Count == 0)
        {
            IsPlayerInside = false;
            SpawnZoneRegistry.NotifyExited(this);
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        // 沿父级链向上查找：collider 自身、任意祖先（一直到根）任一带 playerTag 即视为玩家。
        // 这样即便 Player Tag 设在中间层（比如带 collider 的子节点），而非根节点上，也能匹配。
        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag))
                return true;
            t = t.parent;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmoAlways)
            return;
        DrawZoneGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmoAlways)
            return;
        DrawZoneGizmo();
    }

    private void DrawZoneGizmo()
    {
        Collider c = GetComponent<Collider>();
        if (c == null)
            return;

        Color fill = gizmoColor;
        Color outline = new Color(fill.r, fill.g, fill.b, Mathf.Clamp01(fill.a + 0.4f));

        if (c is BoxCollider box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = fill;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = outline;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
        else if (c is SphereCollider sphere)
        {
            Vector3 scale = transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            Vector3 center = transform.position + transform.rotation * sphere.center;
            Gizmos.color = fill;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = outline;
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
