using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class HarvestableResource : MonoBehaviour, IDamageable
{
    private const string DatabaseAddress = "HarvestResourceDatabase";
    private const string GameSoundGroup = "Game";
    private const string TreeHitSoundName = "WoodHit1";
    private const string OreHitSoundName = "OreHit";

    [Header("Config")]
    [SerializeField] private HarvestResourceDatabaseSO database;
    [SerializeField] private string resourceId = "tree_common";
    [SerializeField] private HarvestResourceType fallbackResourceType = HarvestResourceType.Tree;
    [SerializeField] private bool loadDatabaseFromAddressables = true;

    [Header("Depleted")]
    [SerializeField] private bool destroyWhenDepleted = true;
    [SerializeField] private float destroyDelay = 0.08f;

    [Header("Hit Feedback")]
    [SerializeField] private Transform shakeRoot;
    [SerializeField] private float shakeDuration = 0.16f;
    [SerializeField] private float shakeAngle = 4.5f;
    [SerializeField] private float shakeOffset = 0.035f;

    private HarvestResourceData runtimeData;
    private int currentHealth;
    private bool initialized;
    private bool depleted;
    private Coroutine shakeRoutine;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    public HarvestResourceData Data => runtimeData;
    public int CurrentHealth => currentHealth;
    public bool IsDepleted => depleted;
    public string ResourceId => resourceId;

    /// <summary>
    /// 资源被挖光（包含 destroyDelay 之后）时触发，参数为自身。
    /// 池化使用：订阅者收到事件后将其归还对象池。
    /// </summary>
    public event Action<HarvestableResource> OnDepleted;

    /// <summary>
    /// 池化场景下调用此方法将 destroyWhenDepleted 设为 false，
    /// 让对象池接管生命周期（避免被 Destroy）。
    /// </summary>
    public void SetDestroyWhenDepleted(bool value)
    {
        destroyWhenDepleted = value;
    }

    private void Awake()
    {
        if (shakeRoot == null)
        {
            shakeRoot = transform;
        }

        CacheOriginalTransform();
    }

    private void OnEnable()
    {
        initialized = false;
        depleted = false;
        ResolveData();
        LoadDatabaseAsync().Forget();
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeAngle = Mathf.Max(0f, shakeAngle);
        shakeOffset = Mathf.Max(0f, shakeOffset);
    }

    public void TakeDamage(AttackDamageInfo damageInfo)
    {
        ResolveData();
        if (runtimeData == null || depleted)
        {
            return;
        }

        WeaponItem activeTool = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;
        if (!CanBeDamagedBy(activeTool))
        {
            return;
        }

        InitializeHealthIfNeeded();

        int previousHealth = currentHealth;
        int damage = Mathf.Max(1, damageInfo.damage);
        currentHealth = Mathf.Max(0, currentHealth - damage);
        PlayHitSound(damageInfo.hitPoint);

        List<HarvestDropResult> drops = runtimeData.RollDropsForPassedStages(previousHealth, currentHealth);
        if (drops.Count > 0)
        {
            Vector3 origin = ResolveDropOrigin(damageInfo.hitPoint);
            HarvestDropSpawner.SpawnDropsAsync(drops, origin).Forget();
        }

        PlayShake(damageInfo.hitDirection);

        if (currentHealth <= 0)
        {
            depleted = true;
            StartCoroutine(DepleteRoutine());
        }
    }

    private void PlayHitSound(Vector3 hitPoint)
    {
        if (runtimeData == null)
        {
            return;
        }

        string soundName = runtimeData.resourceType == HarvestResourceType.Tree
            ? TreeHitSoundName
            : IsOreResource(runtimeData.resourceType) ? OreHitSoundName : null;

        if (!string.IsNullOrEmpty(soundName))
        {
            GameMgr.Audio?.PlayAt(GameSoundGroup, soundName, ResolveDropOrigin(hitPoint));
        }
    }

    private bool IsOreResource(HarvestResourceType resourceType)
    {
        return resourceType == HarvestResourceType.IronOre
            || resourceType == HarvestResourceType.SilverOre
            || resourceType == HarvestResourceType.GoldOre;
    }

    private async UniTaskVoid LoadDatabaseAsync()
    {
        if (!loadDatabaseFromAddressables || database != null || GameMgr.AssetLoader == null)
        {
            return;
        }

        HarvestResourceDatabaseSO loaded = await GameMgr.AssetLoader.LoadAsset<HarvestResourceDatabaseSO>(DatabaseAddress);
        if (this == null || loaded == null || database != null)
        {
            return;
        }

        database = loaded;
        ResolveData();
    }

    private void ResolveData()
    {
        HarvestResourceData data = null;

        if (database != null)
        {
            data = database.GetResource(resourceId);
            if (data == null)
            {
                data = database.GetResource(fallbackResourceType);
            }
        }

        runtimeData = data;
        if (runtimeData != null)
        {
            runtimeData.ApplyDefaults();
            InitializeHealthIfNeeded();
        }
    }

    private void InitializeHealthIfNeeded()
    {
        if (initialized || runtimeData == null)
        {
            return;
        }

        currentHealth = Mathf.Max(1, runtimeData.maxHealth);
        initialized = true;
    }

    private bool CanBeDamagedBy(WeaponItem activeTool)
    {
        if (activeTool == null || !activeTool.IsTool || activeTool.toolType != runtimeData.requiredTool)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(runtimeData.targetLayerName))
        {
            return true;
        }

        int configuredLayer = LayerMask.NameToLayer(runtimeData.targetLayerName);
        return configuredLayer < 0 || IsOnConfiguredLayer(configuredLayer);
    }

    private bool IsOnConfiguredLayer(int configuredLayer)
    {
        if (gameObject.layer == configuredLayer)
        {
            return true;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider childCollider = colliders[i];
            if (childCollider != null && childCollider.gameObject.layer == configuredLayer)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ResolveDropOrigin(Vector3 hitPoint)
    {
        if (hitPoint.sqrMagnitude > 0.0001f)
        {
            return hitPoint;
        }

        Collider resourceCollider = GetComponentInChildren<Collider>();
        return resourceCollider != null ? resourceCollider.bounds.center : transform.position;
    }

    private void PlayShake(Vector3 hitDirection)
    {
        if (shakeRoot == null || shakeDuration <= 0f || (shakeAngle <= 0f && shakeOffset <= 0f))
        {
            return;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreOriginalTransform();
        }

        shakeRoutine = StartCoroutine(ShakeRoutine(hitDirection));
    }

    private IEnumerator ShakeRoutine(Vector3 hitDirection)
    {
        CacheOriginalTransform();

        Vector3 direction = hitDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = -transform.forward;
        }

        direction.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        if (axis.sqrMagnitude <= 0.0001f)
        {
            axis = Vector3.right;
        }

        axis.Normalize();

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, shakeDuration));
            float fade = 1f - t;
            float wave = Mathf.Sin(t * Mathf.PI * 4f) * fade;

            shakeRoot.localRotation = originalLocalRotation * Quaternion.AngleAxis(wave * shakeAngle, axis);
            shakeRoot.localPosition = originalLocalPosition + direction * (wave * shakeOffset);
            yield return null;
        }

        RestoreOriginalTransform();
        shakeRoutine = null;
    }

    private IEnumerator DepleteRoutine()
    {
        if (destroyDelay > 0f)
        {
            yield return new WaitForSeconds(destroyDelay);
        }

        // 在 Destroy / SetActive(false) 之前触发事件，
        // 让池化订阅者有机会先 Return 到对象池
        OnDepleted?.Invoke(this);

        if (destroyWhenDepleted)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void CacheOriginalTransform()
    {
        if (shakeRoot == null)
        {
            return;
        }

        originalLocalPosition = shakeRoot.localPosition;
        originalLocalRotation = shakeRoot.localRotation;
    }

    private void RestoreOriginalTransform()
    {
        if (shakeRoot == null)
        {
            return;
        }

        shakeRoot.localPosition = originalLocalPosition;
        shakeRoot.localRotation = originalLocalRotation;
    }
}
