using System.Collections.Generic;
using UnityEngine;

public sealed class BossProjectileRuntimeController : MonoBehaviour
{
    private const int MaxHitResults = 32;
    private const string DefaultTargetLayerName = "Player";

    private readonly Collider[] hitResults = new Collider[MaxHitResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly List<GameObject> externalVisuals = new List<GameObject>();

    private BossProjectileConfig config;
    private LayerMask targetLayers;
    private GameObject attacker;
    private Vector3 origin;
    private Vector3 direction;
    private Quaternion rotation;
    private int damage;
    private float elapsed;
    private bool initialized;
    private bool visualOnly;
    private bool drawDebugGizmos;
    private Color debugGizmoColor = new Color(1f, 0.62f, 0.12f, 0.85f);
    private Vector3 lastLogicCenter;
    private string poolKey;
    private VFXHandle loopingVfxHandle;

    internal string PoolKey => poolKey;

    public void Initialize(BossProjectileConfig projectileConfig, Vector3 spawnPosition, Quaternion spawnRotation, LayerMask damageLayers, int attackDamage, GameObject attackOwner)
    {
        config = projectileConfig;
        origin = spawnPosition;
        rotation = spawnRotation;
        direction = spawnRotation * Vector3.forward;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        targetLayers = damageLayers;
        damage = Mathf.Max(1, attackDamage);
        attacker = attackOwner;
        elapsed = 0f;
        initialized = true;
        visualOnly = false;
        hitTargets.Clear();
        lastLogicCenter = origin + rotation * config.hitCenterOffset;
        transform.SetPositionAndRotation(origin, rotation);
        gameObject.SetActive(true);
    }

    public void InitializeVisualOnly(BossProjectileConfig projectileConfig, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        config = projectileConfig;
        origin = spawnPosition;
        rotation = spawnRotation;
        direction = spawnRotation * Vector3.forward;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        targetLayers = default;
        damage = 0;
        attacker = null;
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
        poolKey = string.IsNullOrWhiteSpace(key) ? "BossProjectile" : key;
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

    public void AttachLoopingVfx(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || GameMgr.VFX == null)
        {
            return;
        }

        loopingVfxHandle?.Stop();
        loopingVfxHandle = GameMgr.VFX.PlayLooping(key, transform);
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
        if (visualOnly)
        {
            return;
        }

        ApplyDamage(hitCenter);
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
        int hitCount = config.hitShape == WeaponProjectileHitShape.Cylinder
            ? OverlapCylinder(center)
            : Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.01f, config.hitRadius), hitResults, GetResolvedTargetLayerMask(), QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - origin).normalized;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = direction;
            }

            if (NetworkBoss.TryApplyBossDamageToNetworkPlayer(hit, damage, hitPoint, hitDirection, attacker))
            {
                PlayHitVfx(hitPoint, hitDirection);
                ReturnToPool();
                return;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable) || damageable is BossHealth || damageable is MonsterHealth)
            {
                continue;
            }

            hitTargets.Add(damageable);
            damageable.TakeDamage(new AttackDamageInfo(damage, hitPoint, hitDirection, attacker));
            PlayHitVfx(hitPoint, hitDirection);
            ReturnToPool();
            return;
        }
    }

    private void PlayHitVfx(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.hitVFXKey) || GameMgr.VFX == null)
        {
            return;
        }

        Quaternion hitRotation = CreateLookRotation(hitDirection, rotation);
        hitRotation *= Quaternion.Euler(GetRandomEuler(config.hitVFXRandomEulerRange));
        GameMgr.VFX.Play(config.hitVFXKey, hitPoint, hitRotation);
    }

    private static Quaternion CreateLookRotation(Vector3 forward, Quaternion fallback)
    {
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return fallback;
        }

        Vector3 normalizedForward = forward.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(normalizedForward, up);
    }

    private int OverlapCylinder(Vector3 center)
    {
        float radius = Mathf.Max(0.01f, config.hitRadius);
        float height = Mathf.Max(0.01f, config.hitHeight);
        Vector3 halfHeight = (rotation * Vector3.up) * (height * 0.5f);
        return Physics.OverlapCapsuleNonAlloc(center - halfHeight, center + halfHeight, radius, hitResults, GetResolvedTargetLayerMask(), QueryTriggerInteraction.Collide);
    }

    private int GetResolvedTargetLayerMask()
    {
        if (targetLayers.value != 0)
        {
            return targetLayers.value;
        }

        int playerMask = LayerMask.GetMask(DefaultTargetLayerName);
        return playerMask != 0 ? playerMask : Physics.AllLayers;
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

    private void ReturnToPool()
    {
        BossProjectilePool.Return(this);
    }

    internal void ResetForPool()
    {
        initialized = false;
        config = null;
        attacker = null;
        elapsed = 0f;
        damage = 0;
        visualOnly = false;
        targetLayers = default;
        direction = Vector3.forward;
        rotation = Quaternion.identity;
        origin = Vector3.zero;
        lastLogicCenter = Vector3.zero;
        hitTargets.Clear();
        drawDebugGizmos = false;
        loopingVfxHandle?.Stop();
        loopingVfxHandle = null;

        for (int i = externalVisuals.Count - 1; i >= 0; i--)
        {
            BossProjectilePool.ReturnVisual(externalVisuals[i]);
        }

        externalVisuals.Clear();
        transform.SetParent(BossProjectilePool.Root, false);
        gameObject.SetActive(false);
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

    private static Vector3 GetRandomEuler(Vector3 range)
    {
        return new Vector3(
            Random.Range(-Mathf.Abs(range.x), Mathf.Abs(range.x)),
            Random.Range(-Mathf.Abs(range.y), Mathf.Abs(range.y)),
            Random.Range(-Mathf.Abs(range.z), Mathf.Abs(range.z)));
    }
}

public sealed class BossProjectileConfig
{
    public string projectileVFXKey;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;
    public WeaponProjectileMotionMode motionMode = WeaponProjectileMotionMode.CodeDriven;
    public float speed = 12f;
    public float lifeTime = 2f;
    public float visualForwardDistance = 6f;
    public AnimationCurve visualForwardCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public WeaponProjectileHitShape hitShape = WeaponProjectileHitShape.Sphere;
    public float hitRadius = 0.35f;
    public float hitHeight = 1f;
    public Vector3 hitCenterOffset;
}

public static class BossProjectilePool
{
    private static readonly Dictionary<string, Queue<BossProjectileRuntimeController>> projectiles = new Dictionary<string, Queue<BossProjectileRuntimeController>>();
    private static readonly Dictionary<int, Queue<GameObject>> visuals = new Dictionary<int, Queue<GameObject>>();

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

    public static BossProjectileRuntimeController Get(string key)
    {
        EnsureRoot();
        key = string.IsNullOrWhiteSpace(key) ? "BossProjectile" : key;
        if (projectiles.TryGetValue(key, out Queue<BossProjectileRuntimeController> queue))
        {
            while (queue.Count > 0)
            {
                BossProjectileRuntimeController projectile = queue.Dequeue();
                if (projectile != null)
                {
                    projectile.SetupPoolKey(key);
                    return projectile;
                }
            }
        }

        GameObject obj = new GameObject("BossProjectile_" + key);
        obj.transform.SetParent(projectileRoot, false);
        obj.SetActive(false);
        BossProjectileRuntimeController controller = obj.AddComponent<BossProjectileRuntimeController>();
        controller.SetupPoolKey(key);
        return controller;
    }

    public static void Return(BossProjectileRuntimeController projectile)
    {
        if (projectile == null)
        {
            return;
        }

        EnsureRoot();
        string key = string.IsNullOrWhiteSpace(projectile.PoolKey) ? "BossProjectile" : projectile.PoolKey;
        projectile.ResetForPool();
        if (!projectiles.TryGetValue(key, out Queue<BossProjectileRuntimeController> queue))
        {
            queue = new Queue<BossProjectileRuntimeController>();
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
        BossProjectilePooledVisual marker = instance.GetComponent<BossProjectilePooledVisual>();
        if (marker == null)
        {
            marker = instance.AddComponent<BossProjectilePooledVisual>();
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
        BossProjectilePooledVisual marker = visual.GetComponent<BossProjectilePooledVisual>();
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

        GameObject rootObject = new GameObject("[BossProjectilePool]");
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
        DisableVisualPhysics(visual);
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

    private static void DisableVisualPhysics(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
            rigidbodies[i].velocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
        }
    }
}

public sealed class BossProjectilePooledVisual : MonoBehaviour
{
    public int PrefabId { get; set; }
}

public sealed class BossProjectileVisualRotationStabilizer : MonoBehaviour
{
    private Transform[] transforms;
    private Quaternion[] localRotations;

    public void Capture()
    {
        transforms = GetComponentsInChildren<Transform>(true);
        localRotations = new Quaternion[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            localRotations[i] = transforms[i].localRotation;
        }
    }

    private void LateUpdate()
    {
        if (transforms == null || localRotations == null || transforms.Length != localRotations.Length)
        {
            return;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                transforms[i].localRotation = localRotations[i];
            }
        }
    }
}
