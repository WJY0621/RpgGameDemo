using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BossHealth))]
[RequireComponent(typeof(BossAttackController))]
[RequireComponent(typeof(BossPhaseController))]
[RequireComponent(typeof(BossSkillRuntimePlayer))]
[RequireComponent(typeof(BossAggroController))]
[RequireComponent(typeof(NavMeshAgent))]
public class BossController : MonoBehaviour
{
    private const string DefaultBossLayerName = "Monster";
    private const string DefaultBossBattleBGMName = "BGM_BOSSBattle";

    [Header("Config")]
    [SerializeField] private BossConfigSO config;

    [Header("Runtime")]
    [SerializeField] private BossStateType currentStateType;
    [SerializeField] private bool battleActive;

    [Header("Reset")]
    [SerializeField, Min(0f)] private float returnHomeAfterPlayerDeathDelay = 3f;

    [Header("Fallback Stats")]
    [SerializeField] private int fallbackMaxHP = 300;
    [SerializeField] private int fallbackAttack = 20;
    [SerializeField] private int fallbackDefense;
    [SerializeField] private float fallbackMoveSpeed = 3.5f;
    [SerializeField] private float fallbackDetectRange = 18f;
    [SerializeField] private float fallbackLoseTargetRange = 30f;
    [SerializeField] private float fallbackPreferredCombatRange = 4f;
    [SerializeField] private float fallbackSkillDecisionInterval = 0.25f;
    [SerializeField] private bool fallbackCanMove = true;
    [SerializeField] private bool fallbackEnterHurtStateOnQuarterHpLoss = true;
    [SerializeField, Range(0.05f, 1f)] private float fallbackHurtHpLossStep = 0.25f;

    [Header("Animation Fallbacks")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string idleBattleStateName = "IdleBattle";
    [SerializeField] private string playerDeathIdleStateName = "IdleNormal";
    [SerializeField] private string runStateName = "Run";
    [SerializeField] private string hurtStateName = "GetHit";
    [SerializeField] private string deadStateName = "Die";
    [SerializeField] private float hurtFinishedNormalizedTime = 0.9f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Skills")]
    [SerializeField] private List<BossSkillSO> fallbackSkills = new List<BossSkillSO>();

    [Header("Runtime Layers")]
    [SerializeField] private bool forceHierarchyToMonsterLayer = true;

    [Header("Battle BGM")]
    [SerializeField] private bool switchBGMOnBattle = true;
    [SerializeField] private string bossBattleBGMName = DefaultBossBattleBGMName;
    [SerializeField, Range(0f, 1f)] private float bossBattleBGMVolume = 1f;
    [SerializeField, Min(0f)] private float bossBattleBGMFadeDuration = 1f;

    private readonly List<BossSkillRuntime> skillRuntimes = new List<BossSkillRuntime>();
    private static int activeBossBattleBGMUsers;
    private static bool hasPreviousBossBGM;
    private static string previousBossBGMName;
    private static float previousBossBGMVolume = 1f;

    private BossStateMachine stateMachine;
    private BossHealth health;
    private BossAttackController attackController;
    private BossPhaseController phaseController;
    private BossSkillRuntimePlayer skillRuntimePlayer;
    private BossAggroController aggroController;
    private Animator animator;
    private NavMeshAgent navAgent;
    private Transform target;
    private BossSkillSO activeSkill;
    private BossPhaseConfig pendingPhase;
    private bool battleEndedRaised;
    private bool runtimeMovementLocked;
    private float nextHurtReactionNormalizedHp;
    private int pendingHurtReactions;
    private bool battleBGMActive;
    private bool deathChestSpawned;
    private PlayerHealth trackedPlayerHealth;
    private Vector3 homePosition;
    private Quaternion homeRotation = Quaternion.identity;
    private bool hasHomeTransform;
    private int returnHomeRequestVersion;

    public BossConfigSO Config => config;
    public BossHealth Health => health;
    public BossAttackController AttackController => attackController;
    public BossPhaseController PhaseController => phaseController;
    public BossSkillRuntimePlayer SkillRuntimePlayer => skillRuntimePlayer;
    public BossAggroController AggroController => aggroController;
    public Transform Target => target;
    public BossSkillSO ActiveSkill => activeSkill;
    public BossPhaseConfig PendingPhase => pendingPhase;
    public bool IsBattleActive => battleActive;
    public bool AutoStartOnDetect => config != null ? config.autoStartOnDetect : false;
    public bool EnterHurtStateOnDamage => config != null ? config.enterHurtStateOnDamage : false;
    public bool EnterHurtStateOnQuarterHpLoss => config != null ? config.enterHurtStateOnQuarterHpLoss : fallbackEnterHurtStateOnQuarterHpLoss;
    public float HurtHpLossStep => Mathf.Clamp(config != null ? config.hurtHpLossStep : fallbackHurtHpLossStep, 0.05f, 1f);
    public int CurrentAttack => Mathf.Max(1, Mathf.RoundToInt(GetBaseAttack() * GetPhaseAttackMultiplier()));
    public float CurrentMoveSpeed => Mathf.Max(0f, GetBaseMoveSpeed() * GetPhaseMoveSpeedMultiplier());
    public float DetectRange => config != null ? config.detectRange : fallbackDetectRange;
    public float LoseTargetRange => config != null ? config.loseTargetRange : fallbackLoseTargetRange;
    public float PreferredCombatRange => config != null ? config.preferredCombatRange : fallbackPreferredCombatRange;
    public float SkillDecisionInterval => Mathf.Max(0.05f, config != null ? config.skillDecisionInterval : fallbackSkillDecisionInterval);
    public bool CanMove => !runtimeMovementLocked && (config != null ? config.canMove : fallbackCanMove);
    public string IdleStateName => config != null ? config.idleStateName : idleStateName;
    public string IdleBattleStateName => config != null ? config.idleBattleStateName : idleBattleStateName;
    public string PlayerDeathIdleStateName => !string.IsNullOrWhiteSpace(playerDeathIdleStateName) ? playerDeathIdleStateName : IdleStateName;
    public string RunStateName => config != null ? config.runStateName : runStateName;
    public string HurtStateName => config != null ? config.hurtStateName : hurtStateName;
    public string DeadStateName => config != null ? config.deadStateName : deadStateName;
    public float HurtFinishedNormalizedTime => config != null ? config.hurtFinishedNormalizedTime : hurtFinishedNormalizedTime;
    public string DisplayName => config != null && !string.IsNullOrWhiteSpace(config.bossName) ? config.bossName : gameObject.name;

    public static event Action<BossController> OnAnyBattleStarted;
    public static event Action<BossController, bool> OnAnyBattleEnded;
    public event Action<BossController> OnBattleStarted;
    public event Action<BossController, bool> OnBattleEnded;
    public event Action<BossController, int> OnSkillAnimationEvent;

    private void Awake()
    {
        ApplyBossLayerToHierarchy();
        CacheComponents();
        BindCombatComponents();
        ApplyConfig();
        BuildSkillRuntimes();
        InitializeStateMachine();
        CachePlayerTarget();
        SetHomeTransform(transform.position, transform.rotation);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
        }

        BindPlayerDeathEvent();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }

        UnbindPlayerDeathEvent();
        returnHomeRequestVersion++;
        EndBossBattleBGM();
    }

    private void Update()
    {
        BindPlayerDeathEvent();
        RefreshAggroTarget(Time.deltaTime);

        if (target == null || IsDeadPlayerTarget(target))
        {
            CachePlayerTarget();
        }

        TickSkillCooldowns(Time.deltaTime);

        if (battleActive && currentStateType != BossStateType.PhaseChange && currentStateType != BossStateType.Dead)
        {
            phaseController?.Tick();
        }

        stateMachine?.Tick(Time.deltaTime);
        currentStateType = stateMachine != null ? stateMachine.CurrentStateType : BossStateType.Inactive;
        TryStartQueuedHurtReaction();
        currentStateType = stateMachine != null ? stateMachine.CurrentStateType : BossStateType.Inactive;
    }

    public void StartBattle()
    {
        returnHomeRequestVersion++;
        EnsureHierarchyActive();

        if (health != null && health.IsDead)
        {
            return;
        }

        CachePlayerTarget();
        if (!HasValidTarget())
        {
            return;
        }

        battleActive = true;
        battleEndedRaised = false;
        aggroController?.RegisterTarget(target, 1f);
        health?.SetInvincible(false);
        BeginBossBattleBGM();
        OnBattleStarted?.Invoke(this);
        OnAnyBattleStarted?.Invoke(this);
        ChangeState(BossStateType.Chase);
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

    private void ApplyBossLayerToHierarchy()
    {
        if (!forceHierarchyToMonsterLayer)
        {
            return;
        }

        int monsterLayer = LayerMask.NameToLayer(DefaultBossLayerName);
        if (monsterLayer < 0)
        {
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = monsterLayer;
        }
    }

    private void BeginBossBattleBGM()
    {
        if (!switchBGMOnBattle || battleBGMActive || string.IsNullOrWhiteSpace(bossBattleBGMName) || GameMgr.Audio == null)
        {
            return;
        }

        if (activeBossBattleBGMUsers <= 0)
        {
            activeBossBattleBGMUsers = 0;
            previousBossBGMName = GameMgr.Audio.CurrentBGMName;
            previousBossBGMVolume = Mathf.Clamp01(GameMgr.Audio.CurrentBGMTargetVolume);
            hasPreviousBossBGM = !string.IsNullOrWhiteSpace(previousBossBGMName);
        }

        activeBossBattleBGMUsers++;
        battleBGMActive = true;
        GameMgr.Audio.PlayBGM(bossBattleBGMName, bossBattleBGMVolume, bossBattleBGMFadeDuration);
    }

    private void EndBossBattleBGM()
    {
        if (!battleBGMActive)
        {
            return;
        }

        battleBGMActive = false;
        activeBossBattleBGMUsers = Mathf.Max(0, activeBossBattleBGMUsers - 1);
        if (activeBossBattleBGMUsers > 0 || GameMgr.Audio == null)
        {
            return;
        }

        if (hasPreviousBossBGM &&
            !string.IsNullOrWhiteSpace(previousBossBGMName) &&
            !string.Equals(previousBossBGMName, bossBattleBGMName, StringComparison.Ordinal))
        {
            GameMgr.Audio.PlayBGM(previousBossBGMName, previousBossBGMVolume, bossBattleBGMFadeDuration);
        }
        else
        {
            GameMgr.Audio.PlaySceneBGM(SceneManager.GetActiveScene().name);
        }

        hasPreviousBossBGM = false;
        previousBossBGMName = null;
        previousBossBGMVolume = 1f;
    }

    public void EndBattle(bool bossDefeated)
    {
        if (!battleActive && battleEndedRaised)
        {
            return;
        }

        battleActive = false;
        aggroController?.Clear();
        activeSkill = null;
        attackController?.EndAttackWindow();
        skillRuntimePlayer?.Stop();
        StopMovement();
        EndBossBattleBGM();

        if (!battleEndedRaised)
        {
            battleEndedRaised = true;
            OnBattleEnded?.Invoke(this, bossDefeated);
            OnAnyBattleEnded?.Invoke(this, bossDefeated);
        }

        if (!bossDefeated && !IsDead())
        {
            ChangeState(BossStateType.Inactive);
        }
    }

    public void ApplyNetworkBattleState(bool active, bool dead)
    {
        if (dead)
        {
            EndBattle(true);
            return;
        }

        if (battleActive == active)
        {
            return;
        }

        battleActive = active;
        if (active)
        {
            battleEndedRaised = false;
            BeginBossBattleBGM();
            OnBattleStarted?.Invoke(this);
            OnAnyBattleStarted?.Invoke(this);
            return;
        }

        EndBossBattleBGM();

        if (!battleEndedRaised)
        {
            battleEndedRaised = true;
            OnBattleEnded?.Invoke(this, false);
            OnAnyBattleEnded?.Invoke(this, false);
        }
    }

    public void CachePlayerTarget()
    {
        BindPlayerDeathEvent();
        Transform player = GetCurrentPlayerTransform();
        if (player != null && !IsDeadPlayerTarget(player))
        {
            target = player;
            aggroController?.RegisterTarget(target, 1f);
            return;
        }

        if (target != null && IsDeadPlayerTarget(target))
        {
            ClearTarget();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
        {
            return;
        }

        if (IsDeadPlayerTarget(newTarget))
        {
            ClearTarget();
            return;
        }

        target = newTarget;
        aggroController?.RegisterTarget(target, 1f);
    }

    public void ClearTarget()
    {
        target = null;
    }

    private void RefreshAggroTarget(float deltaTime)
    {
        if (aggroController == null || IsDead())
        {
            return;
        }

        Transform aggroTarget = aggroController.TickAndSelectTarget(deltaTime, target, LoseTargetRange);
        if (aggroTarget != null && !IsDeadPlayerTarget(aggroTarget))
        {
            target = aggroTarget;
        }
    }

    public bool HasValidTarget()
    {
        if (target == null)
        {
            return false;
        }

        if (IsDeadPlayerTarget(target))
        {
            ClearTarget();
            return false;
        }

        return true;
    }

    public float GetDistanceToTarget()
    {
        if (target == null)
        {
            return float.MaxValue;
        }

        return Vector3.Distance(transform.position, target.position);
    }

    public bool CanDetectTarget()
    {
        return HasValidTarget() && GetDistanceToTarget() <= DetectRange;
    }

    public bool IsTargetLost()
    {
        return !HasValidTarget() || GetDistanceToTarget() > LoseTargetRange;
    }

    public bool IsDead()
    {
        return health != null && health.IsDead;
    }

    public void ChangeState(BossStateType stateType)
    {
        stateMachine?.ChangeState(stateType);
        currentStateType = stateType;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        if (!CanMove)
        {
            StopMovement();
            return;
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = CurrentMoveSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(targetPosition);
            return;
        }

        RotateTowards(targetPosition, Time.deltaTime);
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.position += direction.normalized * CurrentMoveSpeed * Time.deltaTime;
        }
    }

    public void SetMovementLocked(bool locked)
    {
        runtimeMovementLocked = locked;
        if (locked)
        {
            StopMovement();
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }
            return;
        }

        if (navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = CurrentMoveSpeed;
            navAgent.isStopped = false;
        }
    }

    public void StopMovement()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
        {
            return;
        }

        navAgent.isStopped = true;
        if (navAgent.hasPath)
        {
            navAgent.ResetPath();
        }
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        StopMovement();
        transform.rotation = rotation;
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(position);
        }
        else
        {
            transform.position = position;
        }

        StopMovement();
    }

    public void SetHomeTransform(Vector3 position, Quaternion rotation)
    {
        homePosition = position;
        homeRotation = rotation;
        hasHomeTransform = true;
    }

    public void RotateTowards(Vector3 targetPosition, float deltaTime)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    public bool TryStartBestSkill()
    {
        BossSkillRuntime runtime = SelectSkillRuntime();
        if (runtime == null || runtime.Skill == null)
        {
            return false;
        }

        activeSkill = runtime.Skill;
        runtime.StartCooldown(GetPhaseCooldownMultiplier());
        ChangeState(BossStateType.Skill);
        return true;
    }

    public void ClearActiveSkill()
    {
        skillRuntimePlayer?.Stop();
        activeSkill = null;
    }

    public void RequestPhaseChange(BossPhaseConfig phase)
    {
        if (phase == null || IsDead())
        {
            return;
        }

        pendingPhase = phase;
        RebuildSkillRuntimesForCurrentPhase();
        ChangeState(BossStateType.PhaseChange);
    }

    public void ClearPendingPhase()
    {
        pendingPhase = null;
    }

    public void PlayBaseAnimation(string stateName, float blendDuration = 0.1f)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        animator.CrossFade(stateName, blendDuration);
    }

    public bool HasBaseAnimationFinished(string stateName, float normalizedTimeThreshold)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return true;
        }

        if (animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(stateName))
        {
            return false;
        }

        return stateInfo.normalizedTime >= normalizedTimeThreshold;
    }

    public void HandleSkillAnimationEvent(int eventIndex)
    {
        OnSkillAnimationEvent?.Invoke(this, eventIndex);
    }

    private void CacheComponents()
    {
        health = GetComponent<BossHealth>();
        attackController = GetComponent<BossAttackController>();
        phaseController = GetComponent<BossPhaseController>();
        skillRuntimePlayer = GetComponent<BossSkillRuntimePlayer>();
        aggroController = GetComponent<BossAggroController>();
        animator = GetComponentInChildren<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void BindCombatComponents()
    {
        attackController?.Bind(this);
        phaseController?.Bind(this);
        skillRuntimePlayer?.Bind(this);
        aggroController?.Bind(this);

        if (animator == null)
        {
            return;
        }

        BossAnimationEventRelay relay = animator.GetComponent<BossAnimationEventRelay>();
        if (relay == null)
        {
            relay = animator.gameObject.AddComponent<BossAnimationEventRelay>();
        }

        relay.Bind(this, attackController);
    }

    private void ApplyConfig()
    {
        int hp = config != null ? config.maxHP : fallbackMaxHP;
        int def = config != null ? config.defense : fallbackDefense;
        health?.ConfigureStats(hp, def);

        if (navAgent != null)
        {
            navAgent.speed = CurrentMoveSpeed;
            navAgent.isStopped = !CanMove;
        }

        phaseController?.Initialize(config);
        ResetHurtReactionThresholds();
    }

    private void BuildSkillRuntimes()
    {
        skillRuntimes.Clear();
        List<BossSkillSO> skills = GetCurrentSkillPool();
        for (int i = 0; i < skills.Count; i++)
        {
            BossSkillSO skill = skills[i];
            if (skill == null)
            {
                continue;
            }

            skillRuntimes.Add(new BossSkillRuntime(skill));
        }
    }

    private void RebuildSkillRuntimesForCurrentPhase()
    {
        BuildSkillRuntimes();
    }

    private List<BossSkillSO> GetCurrentSkillPool()
    {
        BossPhaseConfig phase = phaseController != null ? phaseController.CurrentPhase : null;
        if (phase != null && phase.skills != null && phase.skills.Count > 0)
        {
            return phase.skills;
        }

        if (config != null && config.defaultSkills != null && config.defaultSkills.Count > 0)
        {
            return config.defaultSkills;
        }

        return fallbackSkills;
    }

    private BossSkillRuntime SelectSkillRuntime()
    {
        if (!HasValidTarget())
        {
            return null;
        }

        int totalWeight = 0;
        for (int i = 0; i < skillRuntimes.Count; i++)
        {
            BossSkillRuntime runtime = skillRuntimes[i];
            if (runtime == null || runtime.Skill == null || !runtime.IsReady)
            {
                continue;
            }

            if (!runtime.Skill.CanUse(this, target))
            {
                continue;
            }

            totalWeight += Mathf.Max(1, runtime.Skill.weight);
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < skillRuntimes.Count; i++)
        {
            BossSkillRuntime runtime = skillRuntimes[i];
            if (runtime == null || runtime.Skill == null || !runtime.IsReady || !runtime.Skill.CanUse(this, target))
            {
                continue;
            }

            roll -= Mathf.Max(1, runtime.Skill.weight);
            if (roll < 0)
            {
                return runtime;
            }
        }

        return null;
    }

    private void TickSkillCooldowns(float deltaTime)
    {
        for (int i = 0; i < skillRuntimes.Count; i++)
        {
            skillRuntimes[i]?.Tick(deltaTime);
        }
    }

    private void InitializeStateMachine()
    {
        stateMachine = new BossStateMachine();
        stateMachine.RegisterState(new BossInactiveState(this));
        stateMachine.RegisterState(new BossIdleState(this));
        stateMachine.RegisterState(new BossChaseState(this));
        stateMachine.RegisterState(new BossSkillState(this));
        stateMachine.RegisterState(new BossHurtState(this));
        stateMachine.RegisterState(new BossPhaseChangeState(this));
        stateMachine.RegisterState(new BossDeadState(this));
        stateMachine.ChangeState(BossStateType.Inactive);
        currentStateType = stateMachine.CurrentStateType;
    }

    private int GetBaseAttack()
    {
        return config != null ? config.attack : fallbackAttack;
    }

    private float GetBaseMoveSpeed()
    {
        return config != null ? config.moveSpeed : fallbackMoveSpeed;
    }

    private float GetPhaseAttackMultiplier()
    {
        BossPhaseConfig phase = phaseController != null ? phaseController.CurrentPhase : null;
        return phase != null ? Mathf.Max(0f, phase.attackMultiplier) : 1f;
    }

    private float GetPhaseMoveSpeedMultiplier()
    {
        BossPhaseConfig phase = phaseController != null ? phaseController.CurrentPhase : null;
        return phase != null ? Mathf.Max(0f, phase.moveSpeedMultiplier) : 1f;
    }

    private float GetPhaseCooldownMultiplier()
    {
        BossPhaseConfig phase = phaseController != null ? phaseController.CurrentPhase : null;
        return phase != null ? Mathf.Max(0.01f, phase.cooldownMultiplier) : 1f;
    }

    private static Transform GetCurrentPlayerTransform()
    {
        return GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.transform
            : null;
    }

    private static PlayerHealth GetCurrentPlayerHealth()
    {
        PlayerStateDriver player = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        return player != null ? player.GetComponent<PlayerHealth>() : null;
    }

    private static PlayerHealth GetPlayerHealthFromTarget(Transform candidate)
    {
        return candidate != null ? candidate.GetComponentInParent<PlayerHealth>() : null;
    }

    private static bool IsDeadPlayerTarget(Transform candidate)
    {
        PlayerHealth playerHealth = GetPlayerHealthFromTarget(candidate);
        return playerHealth != null && playerHealth.IsDead;
    }

    private static bool IsSameTargetHierarchy(Transform candidate, Transform playerRoot)
    {
        return candidate != null && playerRoot != null &&
               (candidate == playerRoot || candidate.IsChildOf(playerRoot) || playerRoot.IsChildOf(candidate));
    }

    private void BindPlayerDeathEvent()
    {
        PlayerHealth currentPlayerHealth = GetCurrentPlayerHealth();
        if (trackedPlayerHealth == currentPlayerHealth)
        {
            return;
        }

        UnbindPlayerDeathEvent();
        trackedPlayerHealth = currentPlayerHealth;
        if (trackedPlayerHealth != null)
        {
            trackedPlayerHealth.OnDied -= HandlePlayerDied;
            trackedPlayerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void UnbindPlayerDeathEvent()
    {
        if (trackedPlayerHealth != null)
        {
            trackedPlayerHealth.OnDied -= HandlePlayerDied;
            trackedPlayerHealth = null;
        }
    }

    private void HandleDied(BossHealth bossHealth)
    {
        returnHomeRequestVersion++;
        SpawnDeathChestAsync().Forget();
        ChangeState(BossStateType.Dead);
    }

    private void HandlePlayerDied(PlayerHealth playerHealth)
    {
        if (playerHealth == null || IsDead())
        {
            return;
        }

        if (!IsSameTargetHierarchy(target, playerHealth.transform))
        {
            return;
        }

        EnterPlayerDeathIdle();
        ReturnHomeAfterPlayerDeathAsync(++returnHomeRequestVersion).Forget();
    }

    private async UniTaskVoid ReturnHomeAfterPlayerDeathAsync(int requestVersion)
    {
        float delay = Mathf.Max(0f, returnHomeAfterPlayerDeathDelay);
        if (delay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        if (requestVersion != returnHomeRequestVersion || IsDead())
        {
            return;
        }

        ReturnHomeAndRecover();
    }

    private void ReturnHomeAndRecover()
    {
        if (!hasHomeTransform)
        {
            SetHomeTransform(transform.position, transform.rotation);
        }

        ClearTarget();
        pendingHurtReactions = 0;
        ClearPendingPhase();
        EnterPlayerDeathIdle();
        TeleportTo(homePosition, homeRotation);
        phaseController?.Initialize(config);
        RebuildSkillRuntimesForCurrentPhase();
        ResetHurtReactionThresholds();
        health?.SetInvincible(false);
        health?.SetCurrentHP(health.MaxHP);
        EnterPlayerDeathIdle();
    }

    private void EnterPlayerDeathIdle()
    {
        ClearTarget();
        aggroController?.Clear();
        pendingHurtReactions = 0;
        ClearActiveSkill();
        activeSkill = null;
        attackController?.EndAttackWindow();
        skillRuntimePlayer?.Stop();
        StopMovement();
        EndBattle(false);
        ChangeState(BossStateType.Inactive);
        PlayBaseAnimation(PlayerDeathIdleStateName, 0.05f);
    }

    private async UniTaskVoid SpawnDeathChestAsync()
    {
        if (deathChestSpawned || config == null || !config.dropChestOnDeath || !CanSpawnGameplayRewardsHere())
        {
            return;
        }

        deathChestSpawned = true;
        string bossName = DisplayName;
        Vector3 spawnPosition = ResolveDeathChestSpawnPosition();
        Quaternion spawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        GameObject chestPrefab = null;
        if (GameMgr.AssetLoader != null && !string.IsNullOrWhiteSpace(config.deathChestPrefabAddress))
        {
            chestPrefab = await GameMgr.AssetLoader.LoadPrefab(config.deathChestPrefabAddress.Trim());
        }

        if (chestPrefab == null)
        {
            chestPrefab = config.deathChestPrefab;
        }

        if (chestPrefab == null)
        {
            Debug.LogWarning($"[BossController] Boss '{bossName}' died but no death chest prefab could be resolved.");
            return;
        }

        Instantiate(chestPrefab, spawnPosition, spawnRotation);
    }

    private Vector3 ResolveDeathChestSpawnPosition()
    {
        Vector3 spawnPosition = transform.position + config.deathChestSpawnOffset;
        float probeHeight = Mathf.Max(0.1f, config.deathChestGroundProbeHeight);
        float probeDistance = Mathf.Max(probeHeight, config.deathChestGroundProbeDistance);
        Vector3 probeOrigin = spawnPosition + Vector3.up * probeHeight;

        if (Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit hit,
                probeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            spawnPosition = hit.point;
        }

        return spawnPosition;
    }

    private static bool CanSpawnGameplayRewardsHere()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
    }

    private void HandleDamaged(BossHealth bossHealth, AttackDamageInfo damageInfo, int actualDamage)
    {
        if (damageInfo.attacker != null)
        {
            if (aggroController != null && aggroController.AddDamageThreat(damageInfo.attacker, actualDamage))
            {
                Transform bestTarget = aggroController.CurrentBestTarget;
                if (bestTarget != null)
                {
                    target = bestTarget;
                }
            }
            else
            {
                SetTarget(damageInfo.attacker.transform);
            }
        }

        if (!battleActive)
        {
            StartBattle();
        }

        QueueHurtReactionForDamage(bossHealth, actualDamage);

        if (EnterHurtStateOnDamage && pendingHurtReactions <= 0)
        {
            pendingHurtReactions = 1;
        }

        TryStartQueuedHurtReaction();
    }

    private void ResetHurtReactionThresholds()
    {
        pendingHurtReactions = 0;
        nextHurtReactionNormalizedHp = 1f - HurtHpLossStep;
    }

    private void QueueHurtReactionForDamage(BossHealth bossHealth, int actualDamage)
    {
        if (!EnterHurtStateOnQuarterHpLoss || bossHealth == null || actualDamage <= 0)
        {
            return;
        }

        float currentNormalizedHp = bossHealth.NormalizedHP;
        float step = HurtHpLossStep;
        int crossedCount = 0;

        while (nextHurtReactionNormalizedHp > 0.0001f &&
               currentNormalizedHp <= nextHurtReactionNormalizedHp + 0.0001f)
        {
            crossedCount++;
            nextHurtReactionNormalizedHp -= step;
        }

        if (crossedCount > 0)
        {
            pendingHurtReactions += crossedCount;
        }
    }

    private void TryStartQueuedHurtReaction()
    {
        if (pendingHurtReactions <= 0 || IsDead())
        {
            return;
        }

        if (currentStateType == BossStateType.Dead ||
            currentStateType == BossStateType.PhaseChange ||
            currentStateType == BossStateType.Hurt)
        {
            return;
        }

        pendingHurtReactions--;
        ChangeState(BossStateType.Hurt);
    }
}
