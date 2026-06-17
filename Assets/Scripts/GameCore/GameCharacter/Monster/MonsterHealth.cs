using UnityEngine;
using System;

public class MonsterHealth : MonoBehaviour, IDamageable
{
    private const string HitSoundGroupName = "Game";
    private const string HitSoundName = "MonsterHit3";

    [Header("Stats")]
    public int maxHP = 30;
    public int currentHP = 30;
    public int defense = 0;

    [Header("Optional")]
    public Animator animator;
    public string hurtStateName = "GitHit";
    public string deadStateName = "Die";
    public bool destroyOnDeath = true;
    public float destroyDelay = 3f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool disableRigidbodiesOnDeath = true;

    [Header("UI")]
    [SerializeField] private GameObject monsterHPUIRoot;
    [SerializeField] private bool activateHPUIOnStart = true;

    private bool isDead;
    private Collider[] cachedColliders;
    private Rigidbody[] cachedRigidbodies;

    public float NormalizedHP => maxHP <= 0 ? 0f : (float)currentHP / maxHP;
    public bool IsDead => isDead;
    public event Action<MonsterHealth> OnHealthChanged;
    /// <summary>
    /// 受伤事件。第三个参数为减防之后的实际扣血值（用于伤害数字、HUD 等）。
    /// </summary>
    public event Action<MonsterHealth, AttackDamageInfo, int> OnDamaged;
    public event Action<MonsterHealth> OnDied;

    private void Reset()
    {
        destroyOnDeath = true;
        destroyDelay = 3f;
        disableCollidersOnDeath = true;
        disableRigidbodiesOnDeath = true;
    }

    private void Awake()
    {
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (monsterHPUIRoot == null)
        {
            Transform uiRoot = transform.Find("MonsterHPUIRoot");
            if (uiRoot != null)
            {
                monsterHPUIRoot = uiRoot.gameObject;
            }
        }

        CachePhysicsComponents();
        NotifyHealthChanged();
    }

    private void Start()
    {
        if (activateHPUIOnStart && monsterHPUIRoot != null)
        {
            monsterHPUIRoot.SetActive(true);
        }
    }

    public void ConfigureStats(int hp, int def)
    {
        maxHP = Mathf.Max(1, hp);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        if (currentHP <= 0 || currentHP > maxHP)
        {
            currentHP = maxHP;
        }
        defense = Mathf.Max(0, def);
        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void SetCurrentHP(int hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHP);
        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void Heal(int value)
    {
        if (value <= 0 || isDead)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP + value, 0, Mathf.Max(1, maxHP));
        NotifyHealthChanged();
    }

    /// <summary>
    /// 当 buff 改变 maxHP/def 时调用，把 MonsterRuntime 的衍生数值同步到本组件。
    /// </summary>
    public void RefreshStatsFromRuntime()
    {
        MonsterController controller = GetComponentInParent<MonsterController>();
        if (controller == null || controller.Runtime == null)
        {
            return;
        }

        MonsterRuntime runtime = controller.Runtime;
        int newMax = Mathf.Max(1, runtime.GetMaxHP());
        maxHP = newMax;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        defense = runtime.GetDEF();
        isDead = currentHP <= 0;
        NotifyHealthChanged();
    }

    public void TakeDamage(AttackDamageInfo damageInfo)
    {
        if (isDead)
        {
            return;
        }

        int finalDamage = Mathf.Max(1, damageInfo.damage - defense);
        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);
        NotifyHealthChanged();
        OnDamaged?.Invoke(this, damageInfo, finalDamage);

        // 在命中点弹出浮动伤害数字（hitPoint 为零时回退到怪物头顶）
        Vector3 popupPos = damageInfo.hitPoint.sqrMagnitude > 0.0001f
            ? damageInfo.hitPoint
            : transform.position + Vector3.up * 1.5f;
        PlayHitSound(popupPos);
        DamageNumberSpawner.Show(finalDamage, popupPos);

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (disableCollidersOnDeath)
        {
            DisableAllColliders();
        }

        if (disableRigidbodiesOnDeath)
        {
            DisableAllRigidbodies();
        }

        NotifyHealthChanged();
        OnDied?.Invoke(this);

        if (animator != null && !string.IsNullOrEmpty(deadStateName))
        {
            animator.CrossFade(deadStateName, 0.05f);
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void PlayHitSound(Vector3 position)
    {
        GameMgr.Audio?.PlayAt(HitSoundGroupName, HitSoundName, position);
    }

    private void DisableAllColliders()
    {
        CachePhysicsComponents();
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = false;
        }
    }

    private void DisableAllRigidbodies()
    {
        CachePhysicsComponents();
        for (int i = 0; i < cachedRigidbodies.Length; i++)
        {
            Rigidbody rb = cachedRigidbodies[i];
            if (rb == null)
                continue;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void CachePhysicsComponents()
    {
        cachedColliders ??= GetComponentsInChildren<Collider>(true);
        cachedRigidbodies ??= GetComponentsInChildren<Rigidbody>(true);
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(this);
    }
}
