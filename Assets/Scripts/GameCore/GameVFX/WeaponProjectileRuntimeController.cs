using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponProjectileRuntimeController : MonoBehaviour
{
    private const int MaxHitResults = 32;
    private const string DefaultTargetLayerName = "Monster";

    private readonly Collider[] hitResults = new Collider[MaxHitResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly List<GameObject> externalVisuals = new List<GameObject>();

    private WeaponProjectileConfigEntry config;
    private LayerMask targetLayers;
    private GameObject attacker;
    private Vector3 origin;
    private Vector3 direction;
    private Quaternion rotation;
    private int damage;
    private int remainingPierce;
    private float elapsed;
    private bool initialized;
    private bool visualOnly;
    private bool drawDebugGizmos;
    private Color debugGizmoColor = new Color(1f, 0.22f, 0.15f, 0.82f);
    private Vector3 lastLogicCenter;
    private string poolKey;

    internal string PoolKey => poolKey;

    public void Initialize(
        WeaponProjectileConfigEntry projectileConfig,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        LayerMask damageLayers,
        int attackDamage,
        GameObject attackOwner)
    {
        config = projectileConfig;
        origin = spawnPosition;
        rotation = spawnRotation;
        direction = spawnRotation * Vector3.forward;
        if (!config.usePitchDirection)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
            if (!config.usePitchDirection)
            {
                direction.y = 0f;
            }
        }

        direction.Normalize();
        targetLayers = damageLayers;
        damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * Mathf.Max(0f, config.damageMultiplier)));
        attacker = attackOwner;
        remainingPierce = Mathf.Max(0, config.pierceCount);
        elapsed = 0f;
        initialized = true;
        visualOnly = false;
        hitTargets.Clear();
        lastLogicCenter = origin + rotation * config.hitCenterOffset;

        transform.SetPositionAndRotation(origin, rotation);
        gameObject.SetActive(true);
    }

    public void InitializeVisualOnly(
        WeaponProjectileConfigEntry projectileConfig,
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        config = projectileConfig;
        origin = spawnPosition;
        rotation = spawnRotation;
        direction = spawnRotation * Vector3.forward;
        if (!config.usePitchDirection)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
            if (!config.usePitchDirection)
            {
                direction.y = 0f;
            }
        }

        direction.Normalize();
        targetLayers = default;
        damage = 0;
        attacker = null;
        remainingPierce = 0;
        elapsed = 0f;
        initialized = true;
        visualOnly = true;
        hitTargets.Clear();
        lastLogicCenter = origin + rotation * config.hitCenterOffset;

        transform.SetPositionAndRotation(origin, rotation);
        gameObject.SetActive(true);
    }

    internal void SetupPoolKey(string key)
    {
        poolKey = string.IsNullOrWhiteSpace(key) ? "Default" : key;
    }

    public void SetDebugGizmos(bool enabled, Color color)
    {
        drawDebugGizmos = enabled;
        debugGizmoColor = color;
    }

    public void RegisterExternalVisual(GameObject visual)
    {
        if (visual != null && !externalVisuals.Contains(visual))
        {
            externalVisuals.Add(visual);
        }
    }

    private void Update()
    {
        if (!initialized || config == null)
        {
            ReturnToPool();
            return;
        }

        elapsed += Time.deltaTime;
        float lifeTime = Mathf.Max(0.01f, config.lifeTime);
        if (elapsed >= lifeTime)
        {
            ReturnToPool();
            return;
        }

        Vector3 projectilePosition = GetProjectilePosition(lifeTime);
        Vector3 hitCenter = projectilePosition + rotation * config.hitCenterOffset;
        lastLogicCenter = hitCenter;
        transform.SetPositionAndRotation(projectilePosition, rotation);
        if (!visualOnly)
        {
            ApplyDamage(hitCenter);
        }
    }

    private Vector3 GetProjectilePosition(float lifeTime)
    {
        float forwardDistance;
        if (config.motionMode == WeaponProjectileMotionMode.VisualSelfMotion)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / lifeTime);
            AnimationCurve curve = config.visualForwardCurve;
            float distance01 = curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
            forwardDistance = config.visualForwardDistance * distance01;
        }
        else
        {
            forwardDistance = config.speed * elapsed;
        }

        return origin + direction * forwardDistance;
    }

    private void ApplyDamage(Vector3 center)
    {
        int layerMask = GetResolvedTargetLayerMask();
        int hitCount = config.hitShape == WeaponProjectileHitShape.Cylinder
            ? OverlapCylinder(center)
            : Physics.OverlapSphereNonAlloc(
                center,
                Mathf.Max(0.01f, config.hitRadius),
                hitResults,
                layerMask,
                QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit == null)
            {
                continue;
            }

            if (IsSelfCollider(hit))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable))
            {
                continue;
            }

            if (damageable is PlayerHealth)
            {
                continue;
            }

            hitTargets.Add(damageable);

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - origin).normalized;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = direction;
            }

            damageable.TakeDamage(new AttackDamageInfo(damage, hitPoint, hitDirection, attacker));
            if (!IsHarvestableDamageable(damageable))
            {
                PlayHitVfx(hitPoint, hitDirection);
            }

            if (remainingPierce <= 0)
            {
                ReturnToPool();
                return;
            }

            remainingPierce--;
        }
    }

    private void PlayHitVfx(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.hitVFXKey) || GameMgr.VFX == null)
        {
            return;
        }

        Quaternion hitRotation = hitDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(hitDirection.normalized, Vector3.up)
            : rotation;
        hitRotation *= Quaternion.Euler(GetRandomEuler(config.hitVFXRandomEulerRange));
        GameMgr.VFX.Play(config.hitVFXKey, hitPoint, hitRotation);
    }

    private static bool IsHarvestableDamageable(IDamageable damageable)
    {
        return damageable is HarvestableResource;
    }

    private static Vector3 GetRandomEuler(Vector3 range)
    {
        return new Vector3(
            Random.Range(-Mathf.Abs(range.x), Mathf.Abs(range.x)),
            Random.Range(-Mathf.Abs(range.y), Mathf.Abs(range.y)),
            Random.Range(-Mathf.Abs(range.z), Mathf.Abs(range.z)));
    }

    private int OverlapCylinder(Vector3 center)
    {
        float radius = Mathf.Max(0.01f, config.hitRadius);
        float height = Mathf.Max(0.01f, config.hitHeight);
        Vector3 halfHeight = (rotation * Vector3.up) * (height * 0.5f);
        return Physics.OverlapCapsuleNonAlloc(
            center - halfHeight,
            center + halfHeight,
            radius,
            hitResults,
            GetResolvedTargetLayerMask(),
            QueryTriggerInteraction.Collide);
    }

    private int GetResolvedTargetLayerMask()
    {
        if (targetLayers.value != 0)
        {
            return targetLayers.value;
        }

        int monsterMask = LayerMask.GetMask(DefaultTargetLayerName);
        return monsterMask != 0 ? monsterMask : Physics.AllLayers;
    }

    private bool IsSelfCollider(Collider hit)
    {
        if (hit == null || attacker == null)
        {
            return false;
        }

        Transform attackerTransform = attacker.transform;
        return hit.transform == attackerTransform ||
               hit.transform.IsChildOf(attackerTransform) ||
               attackerTransform.IsChildOf(hit.transform);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || config == null)
        {
            return;
        }

        Gizmos.color = debugGizmoColor;
        if (config.hitShape == WeaponProjectileHitShape.Cylinder)
        {
            DrawDebugCylinder(lastLogicCenter);
        }
        else
        {
            Gizmos.DrawWireSphere(lastLogicCenter, Mathf.Max(0.01f, config.hitRadius));
        }

        Gizmos.DrawLine(origin, lastLogicCenter);
        Gizmos.DrawSphere(lastLogicCenter, Mathf.Max(0.03f, config.hitRadius * 0.08f));
    }

    private void ReturnToPool()
    {
        WeaponProjectilePool.Return(this);
    }

    internal void ResetForPool()
    {
        initialized = false;
        visualOnly = false;
        config = null;
        attacker = null;
        elapsed = 0f;
        damage = 0;
        remainingPierce = 0;
        targetLayers = default;
        direction = Vector3.forward;
        rotation = Quaternion.identity;
        origin = Vector3.zero;
        lastLogicCenter = Vector3.zero;
        hitTargets.Clear();
        drawDebugGizmos = false;

        for (int i = externalVisuals.Count - 1; i >= 0; i--)
        {
            WeaponProjectilePool.ReturnVisual(externalVisuals[i]);
        }

        externalVisuals.Clear();
        transform.SetParent(WeaponProjectilePool.Root, false);
        gameObject.SetActive(false);
    }

    private void DrawDebugCylinder(Vector3 center)
    {
        float radius = Mathf.Max(0.01f, config.hitRadius);
        float height = Mathf.Max(0.01f, config.hitHeight);
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 halfHeight = up * (height * 0.5f);
        Vector3 bottom = center - halfHeight;
        Vector3 top = center + halfHeight;

        DrawDebugCircle(bottom, right, forward, radius);
        DrawDebugCircle(top, right, forward, radius);
        Gizmos.DrawLine(bottom + right * radius, top + right * radius);
        Gizmos.DrawLine(bottom - right * radius, top - right * radius);
        Gizmos.DrawLine(bottom + forward * radius, top + forward * radius);
        Gizmos.DrawLine(bottom - forward * radius, top - forward * radius);
    }

    private static void DrawDebugCircle(Vector3 center, Vector3 right, Vector3 forward, float radius)
    {
        const int Segments = 32;
        Vector3 previous = center + right * radius;
        for (int i = 1; i <= Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            Vector3 next = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private void OnDestroy()
    {
        for (int i = externalVisuals.Count - 1; i >= 0; i--)
        {
            if (externalVisuals[i] != null)
            {
                Destroy(externalVisuals[i]);
            }
        }

        externalVisuals.Clear();
    }

}

public static class WeaponProjectilePool
{
    private static readonly Dictionary<string, Queue<WeaponProjectileRuntimeController>> projectiles =
        new Dictionary<string, Queue<WeaponProjectileRuntimeController>>();

    private static readonly Dictionary<int, Queue<GameObject>> visuals =
        new Dictionary<int, Queue<GameObject>>();

    private static Transform root;
    private static Transform projectileRoot;
    private static Transform visualRoot;

    public static Transform Root
    {
        get
        {
            EnsureRoot();
            return root;
        }
    }

    public static WeaponProjectileRuntimeController Get(string key)
    {
        EnsureRoot();
        key = string.IsNullOrWhiteSpace(key) ? "Default" : key;
        if (projectiles.TryGetValue(key, out Queue<WeaponProjectileRuntimeController> queue))
        {
            while (queue.Count > 0)
            {
                WeaponProjectileRuntimeController projectile = queue.Dequeue();
                if (projectile != null)
                {
                    projectile.SetupPoolKey(key);
                    return projectile;
                }
            }
        }

        GameObject obj = new GameObject("WeaponProjectile_" + key);
        obj.transform.SetParent(projectileRoot, false);
        obj.SetActive(false);

        WeaponProjectileRuntimeController controller = obj.AddComponent<WeaponProjectileRuntimeController>();
        controller.SetupPoolKey(key);
        return controller;
    }

    public static void Return(WeaponProjectileRuntimeController projectile)
    {
        if (projectile == null)
        {
            return;
        }

        EnsureRoot();
        string key = string.IsNullOrWhiteSpace(projectile.PoolKey) ? "Default" : projectile.PoolKey;
        projectile.ResetForPool();
        if (!projectiles.TryGetValue(key, out Queue<WeaponProjectileRuntimeController> queue))
        {
            queue = new Queue<WeaponProjectileRuntimeController>();
            projectiles[key] = queue;
        }

        queue.Enqueue(projectile);
    }

    public static GameObject GetVisual(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        EnsureRoot();
        int key = prefab.GetInstanceID();
        if (visuals.TryGetValue(key, out Queue<GameObject> queue))
        {
            while (queue.Count > 0)
            {
                GameObject visual = queue.Dequeue();
                if (visual != null)
                {
                    PrepareVisual(visual);
                    return visual;
                }
            }
        }

        GameObject instance = Object.Instantiate(prefab);
        instance.name = prefab.name;
        WeaponProjectilePooledVisual marker = instance.GetComponent<WeaponProjectilePooledVisual>();
        if (marker == null)
        {
            marker = instance.AddComponent<WeaponProjectilePooledVisual>();
        }

        marker.PrefabId = key;
        PrepareVisual(instance);
        return instance;
    }

    public static void ReturnVisual(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        EnsureRoot();
        WeaponProjectilePooledVisual marker = visual.GetComponent<WeaponProjectilePooledVisual>();
        if (marker == null || marker.PrefabId == 0)
        {
            Object.Destroy(visual);
            return;
        }

        StopVisual(visual);
        visual.transform.SetParent(visualRoot, false);
        visual.SetActive(false);

        if (!visuals.TryGetValue(marker.PrefabId, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            visuals[marker.PrefabId] = queue;
        }

        queue.Enqueue(visual);
    }

    private static void EnsureRoot()
    {
        if (root != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("[WeaponProjectilePool]");
        Object.DontDestroyOnLoad(rootObject);
        root = rootObject.transform;

        projectileRoot = new GameObject("Projectiles").transform;
        projectileRoot.SetParent(root, false);

        visualRoot = new GameObject("Visuals").transform;
        visualRoot.SetParent(root, false);
    }

    private static void PrepareVisual(GameObject visual)
    {
        visual.SetActive(true);
        ParticleSystem[] particles = visual.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    private static void StopVisual(GameObject visual)
    {
        ParticleSystem[] particles = visual.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Clear(true);
        }
    }
}

public sealed class WeaponProjectilePooledVisual : MonoBehaviour
{
    public int PrefabId { get; set; }
}
