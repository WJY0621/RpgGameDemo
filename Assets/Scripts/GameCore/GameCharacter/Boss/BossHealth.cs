using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BossHealth : MonoBehaviour, IDamageable
{
    private const string HitSoundGroupName = "Game";
    private const string HitSoundName = "MonsterHit3";

    [Header("Stats")]
    [SerializeField] private int maxHP = 300;
    [SerializeField] private int currentHP = 300;
    [SerializeField] private int defense;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath;
    [SerializeField] private float destroyDelay = 5f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool disableRigidbodiesOnDeath = true;

    private bool isDead;
    private bool invincible;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int Defense => defense;
    public bool IsDead => isDead;
    public bool IsInvincible => invincible;
    public float NormalizedHP => maxHP <= 0 ? 0f : (float)currentHP / maxHP;

    public event Action<BossHealth> OnHealthChanged;
    public event Action<BossHealth, AttackDamageInfo, int> OnDamaged;
    public event Action<BossHealth> OnDied;

    private void Awake()
    {
        if (GetComponent<NetworkBoss>() == null)
        {
            gameObject.AddComponent<NetworkBoss>();
        }

        currentHP = Mathf.Clamp(currentHP, 0, Mathf.Max(1, maxHP));
        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void ConfigureStats(int hp, int def)
    {
        maxHP = Mathf.Max(1, hp);
        currentHP = maxHP;
        defense = Mathf.Max(0, def);
        isDead = false;
        invincible = false;
        NotifyHealthChanged();
    }

    public void SetInvincible(bool value)
    {
        invincible = value;
    }

    public void SetCurrentHP(int hp)
    {
        currentHP = Mathf.Clamp(hp, 0, Mathf.Max(1, maxHP));
        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void ApplyNetworkState(int hp, int max, bool dead)
    {
        maxHP = Mathf.Max(1, max);
        currentHP = Mathf.Clamp(hp, 0, maxHP);

        if (dead)
        {
            Die();
            return;
        }

        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void TakeDamage(AttackDamageInfo damageInfo)
    {
        if (NetworkBoss.TrySubmitClientBossDamage(this, damageInfo))
        {
            return;
        }

        ApplyDamageInternal(damageInfo, true, out _);
    }

    public int ApplyServerDamage(AttackDamageInfo damageInfo, out Vector3 popupPos)
    {
        return ApplyDamageInternal(damageInfo, true, out popupPos);
    }

    private int ApplyDamageInternal(AttackDamageInfo damageInfo, bool showLocalFeedback, out Vector3 popupPos)
    {
        popupPos = damageInfo.hitPoint.sqrMagnitude > 0.0001f
            ? damageInfo.hitPoint
            : transform.position + Vector3.up * 2f;

        if (isDead || invincible)
        {
            return 0;
        }

        int finalDamage = Mathf.Max(1, damageInfo.damage - defense);
        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);
        NotifyHealthChanged();
        OnDamaged?.Invoke(this, damageInfo, finalDamage);

        if (showLocalFeedback)
        {
            GameMgr.Audio?.PlayAt(HitSoundGroupName, HitSoundName, popupPos);
            DamageNumberSpawner.Show(finalDamage, popupPos);
        }

        if (currentHP <= 0)
        {
            Die();
        }

        return finalDamage;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        invincible = true;

        if (disableCollidersOnDeath)
        {
            SetCollidersEnabled(false);
        }

        if (disableRigidbodiesOnDeath)
        {
            FreezeRigidbodies();
        }

        NotifyHealthChanged();
        OnDied?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private void FreezeRigidbodies()
    {
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(this);
    }
}
