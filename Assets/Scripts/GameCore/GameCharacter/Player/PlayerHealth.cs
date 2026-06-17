using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Runtime Stats")]
    [SerializeField] private int maxHP;
    [SerializeField] private int currentHP;
    [SerializeField] private int currentDefense;
    [SerializeField] private bool initializeFromGameDataOnStart = true;
    [SerializeField] private bool applyBossHitKnockback = true;
    [SerializeField, Min(0f)] private float bossHitKnockbackHorizontalSpeed = 3.5f;
    [SerializeField, Min(0f)] private float bossHitKnockbackVerticalSpeed = 2.4f;

    private bool isDead;
    private bool initializedFromGameData;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int CurrentDefense => currentDefense;
    public bool IsDead => isDead;
    public bool IsInitialized => initializedFromGameData || maxHP > 0;
    public float NormalizedHP => maxHP <= 0 ? 0f : (float)currentHP / maxHP;

    public event Action<PlayerHealth> OnHealthChanged;
    public event Action<PlayerHealth, AttackDamageInfo> OnDamaged;
    public event Action<PlayerHealth> OnDied;

    private void Start()
    {
        if (initializeFromGameDataOnStart && IsLocalGameplayPlayer())
        {
            InitializeFromGameData();
        }
    }

    public void InitializeFromGameData()
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        PlayerData data = EnsurePlayerData();
        if (data == null)
        {
            return;
        }

        maxHP = Mathf.Max(1, data.GetMaxHP());
        currentHP = Mathf.Clamp(data.currentHP, 0, maxHP);
        currentDefense = Mathf.Max(0, data.GetDEF());
        isDead = currentHP <= 0;
        initializedFromGameData = true;
        SyncHPToPlayerData();
        NotifyHealthChanged();
    }

    public void RefreshStatsFromPlayerData()
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data == null)
        {
            return;
        }

        if (!initializedFromGameData)
        {
            InitializeFromGameData();
            return;
        }

        maxHP = Mathf.Max(1, data.GetMaxHP());
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        currentDefense = Mathf.Max(0, data.GetDEF());
        SyncHPToPlayerData();
        NotifyHealthChanged();
    }

    public void SetCurrentHP(int hp)
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        EnsureInitializedBeforeRuntimeChange();
        currentHP = Mathf.Clamp(hp, 0, Mathf.Max(1, maxHP));
        isDead = currentHP <= 0;
        SyncHPToPlayerData();
        NotifyHealthChanged();
    }

    public void Heal(int value)
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        EnsureInitializedBeforeRuntimeChange();
        if (value <= 0 || isDead)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP + value, 0, Mathf.Max(1, maxHP));
        SyncHPToPlayerData();
        NotifyHealthChanged();
    }

    // 减伤公式常数：DR = DEF / (DEF + DefenseConstantK)
    // K=50 时 DEF=50 减伤 50%，与当前装备数值规模（单件 3~40，总 DEF 可达 ~200）匹配。
    // 调大 K 让防御更难堆，调小让防御边际收益提前显现。
    private const float DefenseConstantK = 50f;

    public void TakeDamage(AttackDamageInfo damageInfo)
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        EnsureInitializedBeforeRuntimeChange();
        if (isDead)
        {
            return;
        }

        RefreshStatsFromPlayerData();

        // 百分比减伤（diminishing returns）：DR = DEF / (DEF + K)
        // 防御越高减伤越多但永远不到 100%，避免高 DEF 完全无敌
        float defense = Mathf.Max(0, currentDefense);
        float dr = defense / (defense + DefenseConstantK);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damageInfo.damage * (1f - dr)));

        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, Mathf.Max(1, maxHP));
        isDead = currentHP <= 0;
        SyncHPToPlayerData();
        NotifyHealthChanged();
        OnDamaged?.Invoke(this, damageInfo);
        ApplyBossHitKnockback(damageInfo);

        if (isDead)
        {
            OnDied?.Invoke(this);
        }
    }

    private void ApplyBossHitKnockback(AttackDamageInfo damageInfo)
    {
        if (!applyBossHitKnockback || damageInfo.attacker == null)
        {
            return;
        }

        if (!IsBossDamageSource(damageInfo.attacker))
        {
            return;
        }

        PlayerStateDriver driver = GetComponent<PlayerStateDriver>();
        if (driver == null)
        {
            return;
        }

        Vector3 knockbackDirection = damageInfo.hitDirection;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            knockbackDirection = transform.position - damageInfo.attacker.transform.position;
        }

        driver.ApplyHitReaction(knockbackDirection, bossHitKnockbackHorizontalSpeed, bossHitKnockbackVerticalSpeed);
    }

    private static bool IsBossDamageSource(GameObject attacker)
    {
        return attacker != null &&
               (attacker.GetComponentInParent<BossController>() != null ||
                attacker.GetComponentInParent<BossSkillRuntimePlayer>() != null ||
                attacker.GetComponentInParent<BossProjectileRuntimeController>() != null ||
                attacker.GetComponentInChildren<BossProjectileRuntimeController>() != null);
    }

    private PlayerData EnsurePlayerData()
    {
        if (GameMgr.Instance == null)
        {
            return null;
        }

        if (GameMgr.Instance.playerData == null && GameMgr.Instance.playerInitialData != null)
        {
            GameMgr.Instance.playerData = GameMgr.Instance.playerInitialData.GetPlayerInitialData();
        }

        return GameMgr.Instance.playerData;
    }

    private void SyncHPToPlayerData()
    {
        if (!IsLocalGameplayPlayer())
        {
            return;
        }

        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data == null)
        {
            return;
        }

        data.currentHP = currentHP;
    }

    private void EnsureInitializedBeforeRuntimeChange()
    {
        if (initializedFromGameData)
        {
            return;
        }

        if (maxHP > 0 && currentHP > 0)
        {
            initializedFromGameData = true;
            return;
        }

        InitializeFromGameData();
    }

    private bool IsLocalGameplayPlayer()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(this);
    }
}
