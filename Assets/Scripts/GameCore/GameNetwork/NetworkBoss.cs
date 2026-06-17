using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class NetworkBoss : MonoBehaviour
{
    private const string BossStateMessageName = "WorkDemo.BossState";
    private const string BossSummonMessageName = "WorkDemo.BossSummon";
    private const string BossProjectileVisualMessageName = "WorkDemo.BossProjectileVisual";
    private const float StateSyncInterval = 0.1f;

    private static readonly Dictionary<string, NetworkBoss> BossesById = new Dictionary<string, NetworkBoss>();
    private static bool handlersRegistered;
    private static NetworkManager registeredNetworkManager;

    private BossHealth health;
    private BossController controller;
    private BossAttackController attackController;
    private BossPhaseController phaseController;
    private BossSkillRuntimePlayer skillRuntimePlayer;
    private Animator animator;
    private NavMeshAgent navAgent;
    private string bossId;
    private float nextStateSyncTime;
    private bool clientDisplayModeApplied;
    private int lastClientAnimatorStateHash;
    private bool controllerWasEnabled;
    private bool attackControllerWasEnabled;
    private bool phaseControllerWasEnabled;
    private bool skillRuntimePlayerWasEnabled;
    private bool navAgentWasEnabled;
    private Coroutine clientDeathCleanupRoutine;

    public string BossId => bossId;

    private void Awake()
    {
        health = GetComponent<BossHealth>();
        controller = GetComponent<BossController>();
        attackController = GetComponent<BossAttackController>();
        phaseController = GetComponent<BossPhaseController>();
        skillRuntimePlayer = GetComponent<BossSkillRuntimePlayer>();
        animator = GetComponentInChildren<Animator>(true);
        navAgent = GetComponent<NavMeshAgent>();
        bossId = BuildBossId(transform);

        controllerWasEnabled = controller != null && controller.enabled;
        attackControllerWasEnabled = attackController != null && attackController.enabled;
        phaseControllerWasEnabled = phaseController != null && phaseController.enabled;
        skillRuntimePlayerWasEnabled = skillRuntimePlayer != null && skillRuntimePlayer.enabled;
        navAgentWasEnabled = navAgent != null && navAgent.enabled;
    }

    private void OnEnable()
    {
        RegisterBoss();
        BindHealth();
    }

    private void OnDisable()
    {
        if (clientDeathCleanupRoutine != null)
        {
            StopCoroutine(clientDeathCleanupRoutine);
            clientDeathCleanupRoutine = null;
        }

        UnbindHealth();
        UnregisterBoss();
    }

    private void Update()
    {
        RegisterHandlersIfNeeded();

        if (!IsNetworkSessionActive())
        {
            RestoreLocalAuthorityMode();
            return;
        }

        if (IsServer())
        {
            RestoreLocalAuthorityMode();
            TrySendState(false);
            return;
        }

        ApplyClientDisplayMode();
    }

    public static bool TrySubmitClientBossDamage(BossHealth targetHealth, AttackDamageInfo damageInfo)
    {
        if (targetHealth == null || !IsNetworkSessionActive() || IsServer())
        {
            return false;
        }

        NetworkBoss boss = targetHealth.GetComponent<NetworkBoss>();
        if (boss == null)
        {
            boss = targetHealth.gameObject.AddComponent<NetworkBoss>();
        }

        NetworkPlayer ownerPlayer = FindLocalOwnerNetworkPlayer();
        if (ownerPlayer == null)
        {
            return false;
        }

        ownerPlayer.SubmitBossDamage(boss.BossId, damageInfo);
        return true;
    }

    public static int ApplyServerBossDamage(string bossId, AttackDamageInfo damageInfo, out Vector3 popupPosition)
    {
        popupPosition = damageInfo.hitPoint.sqrMagnitude > 0.0001f ? damageInfo.hitPoint : Vector3.zero;
        if (string.IsNullOrWhiteSpace(bossId) || !IsServer())
        {
            return 0;
        }

        NetworkBoss boss = FindBossById(bossId);
        if (boss == null || boss.health == null)
        {
            return 0;
        }

        int actualDamage = boss.health.ApplyServerDamage(damageInfo, out popupPosition);
        boss.TrySendState(true);
        return actualDamage;
    }

    public static bool TryApplyBossDamageToNetworkPlayer(
        Collider hit,
        int damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        GameObject attacker)
    {
        if (hit == null || !IsServer())
        {
            return false;
        }

        NetworkPlayer networkPlayer = hit.GetComponentInParent<NetworkPlayer>();
        if (networkPlayer == null)
        {
            return false;
        }

        return networkPlayer.TryApplyBossDamageToOwnerFromServer(damage, hitPoint, hitDirection, attacker);
    }

    public static void SendBossSummon(BossSummonArena arena, BossController boss)
    {
        if (!IsServer() || !IsNetworkSessionActive() || arena == null || boss == null)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        BossHealth bossHealth = boss.Health;
        if (bossHealth == null)
        {
            return;
        }

        NetworkBoss networkBoss = boss.GetComponent<NetworkBoss>();
        if (networkBoss == null)
        {
            networkBoss = boss.gameObject.AddComponent<NetworkBoss>();
        }

        ReadAnimatorState(networkBoss.animator, out int animatorStateHash, out float animatorNormalizedTime);

        using FastBufferWriter writer = new FastBufferWriter(400, Allocator.Temp);
        writer.WriteValueSafe(new FixedString128Bytes(arena.ArenaId));
        writer.WriteValueSafe(new FixedString128Bytes(networkBoss.BossId));
        writer.WriteValueSafe(boss.transform.position);
        writer.WriteValueSafe(boss.transform.rotation);
        writer.WriteValueSafe(bossHealth.CurrentHP);
        writer.WriteValueSafe(bossHealth.MaxHP);
        writer.WriteValueSafe(bossHealth.IsDead);
        writer.WriteValueSafe(boss.IsBattleActive);
        writer.WriteValueSafe(animatorStateHash);
        writer.WriteValueSafe(animatorNormalizedTime);
        networkManager.CustomMessagingManager.SendNamedMessageToAll(BossSummonMessageName, writer);
    }

    public static void SendBossProjectileVisual(BossProjectileConfig projectileConfig, string projectileKey, Vector3 position, Quaternion rotation)
    {
        if (!IsServer() ||
            !IsNetworkSessionActive() ||
            projectileConfig == null ||
            string.IsNullOrWhiteSpace(projectileKey))
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(new FixedString128Bytes(projectileKey));
        writer.WriteValueSafe(position);
        writer.WriteValueSafe(rotation);
        writer.WriteValueSafe(projectileConfig.speed);
        writer.WriteValueSafe(projectileConfig.lifeTime);
        writer.WriteValueSafe(projectileConfig.hitCenterOffset);
        networkManager.CustomMessagingManager.SendNamedMessageToAll(BossProjectileVisualMessageName, writer);
    }

    public void ApplyClientDisplayModeNow()
    {
        if (!IsNetworkSessionActive() || IsServer())
        {
            return;
        }

        ApplyClientDisplayMode();
    }

    private void BindHealth()
    {
        if (health == null)
        {
            return;
        }

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnHealthChanged += HandleHealthChanged;
    }

    private void UnbindHealth()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(BossHealth _)
    {
        TrySendState(true);
    }

    private void TrySendState(bool force)
    {
        if (!IsServer() || health == null || !IsNetworkSessionActive())
        {
            return;
        }

        if (!force && Time.unscaledTime < nextStateSyncTime)
        {
            return;
        }

        nextStateSyncTime = Time.unscaledTime + StateSyncInterval;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        ReadAnimatorState(animator, out int animatorStateHash, out float animatorNormalizedTime);

        using FastBufferWriter writer = new FastBufferWriter(272, Allocator.Temp);
        writer.WriteValueSafe(new FixedString128Bytes(bossId));
        writer.WriteValueSafe(transform.position);
        writer.WriteValueSafe(transform.rotation);
        writer.WriteValueSafe(health.CurrentHP);
        writer.WriteValueSafe(health.MaxHP);
        writer.WriteValueSafe(health.IsDead);
        writer.WriteValueSafe(controller != null && controller.IsBattleActive);
        writer.WriteValueSafe(animatorStateHash);
        writer.WriteValueSafe(animatorNormalizedTime);
        networkManager.CustomMessagingManager.SendNamedMessageToAll(BossStateMessageName, writer);
    }

    private void ApplyClientState(
        Vector3 position,
        Quaternion rotation,
        int currentHP,
        int maxHP,
        bool isDead,
        bool isBattleActive,
        int animatorStateHash,
        float animatorNormalizedTime)
    {
        EnsureHierarchyActive();
        ApplyClientDisplayMode();
        transform.SetPositionAndRotation(position, rotation);
        health?.ApplyNetworkState(currentHP, maxHP, isDead);
        controller?.ApplyNetworkBattleState(isBattleActive, isDead);
        ApplyClientAnimatorState(animatorStateHash, animatorNormalizedTime);
        if (isDead)
        {
            ScheduleClientDeathCleanup();
        }
    }

    private void ApplyClientDisplayMode()
    {
        if (clientDisplayModeApplied)
        {
            return;
        }

        clientDisplayModeApplied = true;

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (attackController != null)
        {
            attackController.enabled = false;
        }

        if (phaseController != null)
        {
            phaseController.enabled = false;
        }

        if (skillRuntimePlayer != null)
        {
            skillRuntimePlayer.enabled = false;
        }

        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }

    private void ApplyClientAnimatorState(int stateHash, float normalizedTime)
    {
        if (stateHash == 0)
        {
            return;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (!animator.HasState(0, stateHash))
        {
            return;
        }

        bool shouldApply = lastClientAnimatorStateHash != stateHash;
        if (!shouldApply && !animator.IsInTransition(0))
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            float currentTime = Mathf.Repeat(current.normalizedTime, 1f);
            float targetTime = Mathf.Repeat(normalizedTime, 1f);
            shouldApply = Mathf.Abs(currentTime - targetTime) > 0.25f;
        }

        if (!shouldApply)
        {
            return;
        }

        lastClientAnimatorStateHash = stateHash;
        animator.Play(stateHash, 0, normalizedTime);
    }

    private void ScheduleClientDeathCleanup()
    {
        if (clientDeathCleanupRoutine != null)
        {
            return;
        }

        clientDeathCleanupRoutine = StartCoroutine(ClientDeathCleanupRoutine());
    }

    private IEnumerator ClientDeathCleanupRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        clientDeathCleanupRoutine = null;
        gameObject.SetActive(false);
    }

    private void RestoreLocalAuthorityMode()
    {
        if (!clientDisplayModeApplied)
        {
            return;
        }

        clientDisplayModeApplied = false;

        if (controller != null)
        {
            controller.enabled = controllerWasEnabled;
        }

        if (attackController != null)
        {
            attackController.enabled = attackControllerWasEnabled;
        }

        if (phaseController != null)
        {
            phaseController.enabled = phaseControllerWasEnabled;
        }

        if (skillRuntimePlayer != null)
        {
            skillRuntimePlayer.enabled = skillRuntimePlayerWasEnabled;
        }

        if (navAgent != null)
        {
            navAgent.enabled = navAgentWasEnabled;
        }
    }

    private void RegisterBoss()
    {
        if (!string.IsNullOrWhiteSpace(bossId))
        {
            BossesById[bossId] = this;
        }
    }

    private void UnregisterBoss()
    {
        if (!string.IsNullOrWhiteSpace(bossId) &&
            BossesById.TryGetValue(bossId, out NetworkBoss boss) &&
            boss == this)
        {
            BossesById.Remove(bossId);
        }
    }

    public static void RegisterHandlersIfNeeded()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        if (handlersRegistered && registeredNetworkManager == networkManager)
        {
            return;
        }

        registeredNetworkManager = networkManager;
        handlersRegistered = true;
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BossSummonMessageName);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(BossSummonMessageName, HandleBossSummonMessage);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BossProjectileVisualMessageName);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(BossProjectileVisualMessageName, HandleBossProjectileVisualMessage);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(BossStateMessageName);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(BossStateMessageName, HandleBossStateMessage);
    }

    private static void HandleBossSummonMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer())
        {
            return;
        }

        reader.ReadValueSafe(out FixedString128Bytes arenaId);
        reader.ReadValueSafe(out FixedString128Bytes bossId);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out Quaternion rotation);
        reader.ReadValueSafe(out int currentHP);
        reader.ReadValueSafe(out int maxHP);
        reader.ReadValueSafe(out bool isDead);
        reader.ReadValueSafe(out bool isBattleActive);
        reader.ReadValueSafe(out int animatorStateHash);
        reader.ReadValueSafe(out float animatorNormalizedTime);

        BossController boss = BossSummonArena.PrepareNetworkSummonedBoss(
            arenaId.ToString(),
            position,
            rotation,
            currentHP,
            maxHP,
            isDead,
            isBattleActive);

        NetworkBoss networkBoss = boss != null ? boss.GetComponent<NetworkBoss>() : FindBossById(bossId.ToString());
        if (networkBoss != null)
        {
            networkBoss.ApplyClientState(position, rotation, currentHP, maxHP, isDead, isBattleActive, animatorStateHash, animatorNormalizedTime);
        }
    }

    private static void HandleBossProjectileVisualMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer())
        {
            return;
        }

        reader.ReadValueSafe(out FixedString128Bytes projectileKey);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out Quaternion rotation);
        reader.ReadValueSafe(out float speed);
        reader.ReadValueSafe(out float lifeTime);
        reader.ReadValueSafe(out Vector3 hitCenterOffset);

        string key = projectileKey.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        BossProjectileConfig config = new BossProjectileConfig
        {
            projectileVFXKey = key,
            speed = Mathf.Max(0f, speed),
            lifeTime = Mathf.Max(0.01f, lifeTime),
            hitCenterOffset = hitCenterOffset
        };

        BossProjectileRuntimeController projectile = BossProjectilePool.Get(key);
        projectile.InitializeVisualOnly(config, position, rotation);
        projectile.AttachLoopingVfx(key);
    }

    private static void HandleBossStateMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer())
        {
            return;
        }

        reader.ReadValueSafe(out FixedString128Bytes id);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out Quaternion rotation);
        reader.ReadValueSafe(out int currentHP);
        reader.ReadValueSafe(out int maxHP);
        reader.ReadValueSafe(out bool isDead);
        reader.ReadValueSafe(out bool isBattleActive);
        reader.ReadValueSafe(out int animatorStateHash);
        reader.ReadValueSafe(out float animatorNormalizedTime);

        string key = id.ToString();
        NetworkBoss boss = FindBossById(key);
        if (boss == null && !isDead)
        {
            BossController summonedBoss = BossSummonArena.TryPrepareNetworkBossByBossId(
                key,
                position,
                rotation,
                currentHP,
                maxHP,
                isDead,
                isBattleActive);
            boss = summonedBoss != null ? summonedBoss.GetComponent<NetworkBoss>() : null;
        }

        if (boss != null)
        {
            boss.ApplyClientState(position, rotation, currentHP, maxHP, isDead, isBattleActive, animatorStateHash, animatorNormalizedTime);
        }
    }

    private void EnsureHierarchyActive()
    {
        Transform current = transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }
    }

    private static NetworkBoss FindBossById(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (BossesById.TryGetValue(key, out NetworkBoss boss) && boss != null)
        {
            return boss;
        }

        NetworkBoss[] bosses = Resources.FindObjectsOfTypeAll<NetworkBoss>();
        for (int i = 0; i < bosses.Length; i++)
        {
            NetworkBoss candidate = bosses[i];
            if (candidate == null || candidate.BossId != key)
            {
                continue;
            }

            BossesById[key] = candidate;
            return candidate;
        }

        return null;
    }

    private static NetworkPlayer FindLocalOwnerNetworkPlayer()
    {
        NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayer player = players[i];
            if (player != null && player.IsSpawned && player.IsOwner)
            {
                return player;
            }
        }

        return null;
    }

    private static string BuildBossId(Transform target)
    {
        if (target == null)
        {
            return "Boss";
        }

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void ReadAnimatorState(Animator targetAnimator, out int stateHash, out float normalizedTime)
    {
        stateHash = 0;
        normalizedTime = 0f;
        if (targetAnimator == null || !targetAnimator.isActiveAndEnabled || targetAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
        stateHash = stateInfo.fullPathHash;
        normalizedTime = stateInfo.normalizedTime;
    }

    private static bool IsNetworkSessionActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
