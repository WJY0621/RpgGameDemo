using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private int monsterID;

    [Header("Runtime")]
    [SerializeField] private MonsterRuntime runtime;
    [SerializeField] private MonsterStateType currentStateType;

    [Header("Cached Components")]
    [SerializeField] private MonsterHealth monsterHealth;
    [SerializeField] private MonsterAttackController monsterAttackController;
    [SerializeField] private Animator monsterAnimator;
    [SerializeField] private NavMeshAgent navAgent;

    [Header("Movement")]
    [Tooltip("无 NavMeshAgent / 离开 NavMesh 时使用的回退旋转速度")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float reachDistance = 0.15f;
    [SerializeField] private float navRepathInterval = 0.25f;
    [SerializeField] private float navRepathDistance = 0.6f;
    [SerializeField] private float playerStopRangeRatio = 0.82f;
    [SerializeField] private float minPlayerSeparation = 0.85f;
    [SerializeField] private float navStoppingDistancePadding = 0.05f;

    [Header("Patrol")]
    [SerializeField] private bool usePatrol;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] public float patrolWaitDuration = 1.5f;

    [Header("Attack")]
    [SerializeField] public float attackCooldown = 1.5f;
    [SerializeField] private float attackEnterRangeBuffer = 0.15f;
    [SerializeField] private float attackExitRangeBuffer = 0.6f;
    [SerializeField] private float minAttackStateDuration = 0.35f;

    [Header("Animation")]
    [SerializeField] private float defaultFinishedNormalizedTime = 0.95f;
    [SerializeField] private float hurtFinishedNormalizedTime = 0.9f;
    [SerializeField] private float hurtAnimationStartTimeout = 0.15f;
    public string idleStateName = "Idle";
    public string walkStateName = "Walk";
    public string runStateName = "Run";
    public string tauntStateName = "Taunt";
    public string idleBattleStateName = "IdleBattle";
    public string attackStateName = "Attack";
    public string hurtStateName = "GetHit";
    public string deadStateName = "Die";

    private Transform targetPlayer;
    private Vector3 spawnPosition;
    private int currentPatrolIndex;
    private float attackTimer;
    private MonsterStateMachine stateMachine;
    private bool hasGrantedDropRewards;
    private Collider[] cachedColliders;
    private Rigidbody[] cachedRigidbodies;
    private bool hasCachedPhysicsComponents;
    private Vector3 lastNavDestination;
    private float nextNavRepathTime;
    private bool hasNavDestination;
    private PlayerHealth trackedPlayerHealth;

    public MonsterRuntime Runtime => runtime;
    public MonsterHealth Health => monsterHealth;
    public MonsterAttackController AttackController => monsterAttackController;
    public Animator Animator => monsterAnimator;
    public NavMeshAgent NavAgent => navAgent;
    public Transform TargetPlayer => targetPlayer;
    public int MonsterID => monsterID;
    public Vector3 SpawnPosition => spawnPosition;
    public float MinAttackStateDuration => minAttackStateDuration;
    public float HurtFinishedNormalizedTime => hurtFinishedNormalizedTime;
    public float HurtAnimationStartTimeout => hurtAnimationStartTimeout;

    private void Awake()
    {
        spawnPosition = transform.position;
        CacheComponents();
        CachePhysicsComponents();
        BindCombatComponents();
        InitializeRuntime();
        CachePlayerTarget();
        InitializeStateMachine();
        EnsureBuffComponent();
    }

    private void EnsureBuffComponent()
    {
        BuffComponent buffComponent = GetComponent<BuffComponent>();
        if (buffComponent == null)
        {
            buffComponent = gameObject.AddComponent<BuffComponent>();
        }
        buffComponent.Initialize(new MonsterBuffTarget(this, monsterHealth));
    }

    private void OnEnable()
    {
        CachePlayerTarget();
        BindPlayerDeathEvent();
        if (monsterHealth != null)
        {
            monsterHealth.OnDied -= HandleDead;
            monsterHealth.OnDied += HandleDead;
            monsterHealth.OnHealthChanged -= HandleHealthChanged;
            monsterHealth.OnHealthChanged += HandleHealthChanged;
            monsterHealth.OnDamaged -= HandleDamaged;
            monsterHealth.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (monsterHealth != null)
        {
            monsterHealth.OnDied -= HandleDead;
            monsterHealth.OnHealthChanged -= HandleHealthChanged;
            monsterHealth.OnDamaged -= HandleDamaged;
        }

        UnbindPlayerDeathEvent();
    }

    private void Update()
    {
        if (stateMachine == null)
        {
            return;
        }

        BindPlayerDeathEvent();
        if (targetPlayer == null || IsDeadPlayerTarget(targetPlayer))
        {
            CachePlayerTarget();
        }

        currentStateType = stateMachine.CurrentStateType;
        stateMachine.Tick(Time.deltaTime);
    }

    public void SetMonsterID(int id)
    {
        monsterID = id;
        InitializeRuntime();
    }

    /// <summary>
    /// 从对象池取出时调用，将怪物重置为初始战斗状态。
    /// MonsterPool 内部调用，外部无需手动调用。
    /// </summary>
    public void ResetForSpawn(Vector3 position)
    {
        spawnPosition = position;

        // 重新启用死亡时被禁用的碰撞体
        CachePhysicsComponents();
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = true;
        }

        // 重新启用死亡时被冻结的刚体
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < cachedRigidbodies.Length; i++)
        {
            Rigidbody rb = cachedRigidbodies[i];
            if (rb == null)
                continue;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 重新启用 NavMeshAgent 并瞬移到生成位置（必须用 Warp，直接改 transform.position 会导致 Agent 路径错乱）
        if (navAgent != null)
        {
            if (!navAgent.enabled)
                navAgent.enabled = true;
            navAgent.Warp(position);
            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                if (navAgent.hasPath)
                    navAgent.ResetPath();
            }
        }
        ClearNavDestinationCache();
        InitializeRuntime();

        // 重置数据（同时重置 hasGrantedDropRewards）
        InitializeRuntime();
        CachePlayerTarget();

        // 重置状态机到初始状态
        if (stateMachine != null)
            ChangeState(usePatrol ? MonsterStateType.Patrol : MonsterStateType.Idle);
    }

    public void InitializeRuntime()
    {
        runtime = MonsterJsonDatabase.CreateRuntime(monsterID);
        hasGrantedDropRewards = false;
        if (runtime == null)
        {
            // monsterID==0 视为"未初始化"占位（prefab 实例化瞬间触发的 Awake）；
            // 真正的 ID 会随后由 MonsterPool.CreateMonster 通过 SetMonsterID 注入。
            // 仅在 monsterID 非 0 但数据库找不到时才报警告。
            if (monsterID != 0)
                Debug.LogWarning($"[MonsterController] Monster data not found in MonsterData.json. monsterID={monsterID}, object={name}", this);
            return;
        }

        ApplyRuntimeToComponents();
    }

    public void CachePlayerTarget()
    {
        BindPlayerDeathEvent();
        Transform player = GetCurrentPlayerTransform();
        targetPlayer = player != null && !IsDeadPlayerTarget(player)
            ? player
            : null;
    }

    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (IsDeadPlayerTarget(target))
        {
            ClearTarget();
            return;
        }

        targetPlayer = target;
    }

    public void ClearTarget()
    {
        targetPlayer = null;
    }

    public float GetDistanceToPlayer()
    {
        if (targetPlayer == null)
        {
            return float.MaxValue;
        }

        return Vector3.Distance(transform.position, targetPlayer.position);
    }

    public bool HasValidTarget()
    {
        if (targetPlayer == null)
        {
            return false;
        }

        if (IsDeadPlayerTarget(targetPlayer))
        {
            ClearTarget();
            return false;
        }

        return true;
    }

    public bool CanDetectPlayer()
    {
        return HasValidTarget() && runtime != null && GetDistanceToPlayer() <= runtime.alertRange;
    }

    public bool CanEnterAttackState()
    {
        if (!HasValidTarget() || runtime == null)
        {
            return false;
        }

        float enterRange = Mathf.Max(0.05f, runtime.attackRange + attackEnterRangeBuffer);
        return GetDistanceToPlayer() <= enterRange;
    }

    public bool IsPlayerInAttackRange()
    {
        return HasValidTarget() && runtime != null && GetDistanceToPlayer() <= runtime.attackRange;
    }

    public bool ShouldExitAttackState()
    {
        return !HasValidTarget() || runtime == null || GetDistanceToPlayer() > runtime.attackRange + attackExitRangeBuffer;
    }

    public bool IsPlayerLost()
    {
        return !HasValidTarget() || runtime == null || GetDistanceToPlayer() > runtime.loseTargetRange;
    }

    public bool UsePatrol()
    {
        return usePatrol && patrolPoints != null && patrolPoints.Length > 0;
    }

    public Transform GetCurrentPatrolPoint()
    {
        if (!UsePatrol())
        {
            return null;
        }

        currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, patrolPoints.Length - 1);
        return patrolPoints[currentPatrolIndex];
    }

    public void AdvancePatrolPoint()
    {
        if (!UsePatrol())
        {
            return;
        }

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    public void ChangeState(MonsterStateType stateType)
    {
        stateMachine?.ChangeState(stateType);
        currentStateType = stateType;
    }

    public void MoveTowards(Vector3 targetPosition, float deltaTime)
    {
        if (runtime == null)
        {
            return;
        }

        bool approachingPlayer = IsTargetPlayerPosition(targetPosition);
        Vector3 moveTarget = approachingPlayer ? GetPlayerApproachPosition(targetPosition) : targetPosition;

        // 优先使用 NavMeshAgent 寻路（处理绕障、地形等）
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = runtime.moveSpeed;
            navAgent.stoppingDistance = approachingPlayer ? reachDistance : Mathf.Max(0f, reachDistance);
            navAgent.isStopped = false;
            if (ShouldUpdateNavDestination(moveTarget))
            {
                navAgent.SetDestination(moveTarget);
                lastNavDestination = moveTarget;
                nextNavRepathTime = Time.time + Mathf.Max(0.05f, navRepathInterval);
                hasNavDestination = true;
            }
            return;
        }

        // 回退：未挂 Agent / NavMesh 未烘焙 / Agent 离开 NavMesh 时直线移动
        Vector3 direction = targetPosition - transform.position;
        if (approachingPlayer && GetFlatDistance(transform.position, targetPosition) <= GetPlayerApproachStopDistance())
        {
            RotateTowards(targetPosition, deltaTime);
            return;
        }

        direction = moveTarget - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= reachDistance * reachDistance)
        {
            return;
        }

        RotateTowards(targetPosition, deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, runtime.moveSpeed * deltaTime);
    }

    /// <summary>
    /// 立即停止寻路移动。在 Idle/Attack/Hurt/Taunt/Dead 等不需要位移的状态下调用。
    /// </summary>
    public void StopMovement()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
        {
            ClearNavDestinationCache();
            return;
        }

        navAgent.isStopped = true;
        if (navAgent.hasPath)
        {
            navAgent.ResetPath();
        }
        ClearNavDestinationCache();
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

    public bool ReachedPosition(Vector3 targetPosition)
    {
        Vector3 flatCurrent = transform.position;
        Vector3 flatTarget = targetPosition;
        flatCurrent.y = 0f;
        flatTarget.y = 0f;
        return Vector3.Distance(flatCurrent, flatTarget) <= reachDistance;
    }

    public void TickAttackCooldown(float deltaTime)
    {
        if (attackTimer <= 0f)
        {
            attackTimer = 0f;
            return;
        }

        attackTimer = Mathf.Max(0f, attackTimer - deltaTime);
    }

    public bool IsAttackCooldownReady()
    {
        return attackTimer <= 0f;
    }

    public void StartAttackCooldown()
    {
        attackTimer = attackCooldown;
    }

    public void ResetAttackTimer()
    {
        attackTimer = 0f;
    }

    public void PlayBaseAnimation(string stateName, float blendDuration = 0.1f)
    {
        if (monsterAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        monsterAnimator.CrossFade(stateName, blendDuration);
    }

    public bool IsCurrentBaseAnimation(string stateName)
    {
        if (monsterAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        if (monsterAnimator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo stateInfo = monsterAnimator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName);
    }

    public bool HasBaseAnimationFinished(string stateName)
    {
        return HasBaseAnimationFinished(stateName, defaultFinishedNormalizedTime);
    }

    public bool HasBaseAnimationFinished(string stateName, float normalizedTimeThreshold)
    {
        if (monsterAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return true;
        }

        if (monsterAnimator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo stateInfo = monsterAnimator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(stateName))
        {
            return false;
        }

        return stateInfo.normalizedTime >= normalizedTimeThreshold;
    }

    public bool IsDead()
    {
        return (runtime != null && runtime.isDead) || (monsterHealth != null && monsterHealth.IsDead);
    }

    private void CacheComponents()
    {
        if (monsterHealth == null)
        {
            monsterHealth = GetComponent<MonsterHealth>();
        }

        if (monsterAttackController == null)
        {
            monsterAttackController = GetComponent<MonsterAttackController>();
        }

        if (monsterAnimator == null)
        {
            monsterAnimator = GetComponentInChildren<Animator>();
        }

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
                navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
    }

    private void CachePhysicsComponents()
    {
        if (hasCachedPhysicsComponents)
            return;

        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        hasCachedPhysicsComponents = true;
    }

    private bool ShouldUpdateNavDestination(Vector3 targetPosition)
    {
        if (!hasNavDestination || navAgent == null || !navAgent.hasPath)
            return true;

        float repathDistanceSqr = Mathf.Max(0.01f, navRepathDistance * navRepathDistance);
        if ((targetPosition - lastNavDestination).sqrMagnitude >= repathDistanceSqr)
            return true;

        return Time.time >= nextNavRepathTime && !navAgent.pathPending;
    }

    private void ClearNavDestinationCache()
    {
        hasNavDestination = false;
        nextNavRepathTime = 0f;
        lastNavDestination = default;
    }

    private void BindCombatComponents()
    {
        if (monsterAttackController == null)
        {
            monsterAttackController = gameObject.AddComponent<MonsterAttackController>();
        }

        monsterAttackController.Bind(this);

        if (monsterAnimator == null)
        {
            return;
        }

        MonsterAnimationEventRelay relay = monsterAnimator.GetComponent<MonsterAnimationEventRelay>();
        if (relay == null)
        {
            relay = monsterAnimator.gameObject.AddComponent<MonsterAnimationEventRelay>();
        }

        relay.Bind(monsterAttackController);
    }

    private void ApplyRuntimeToComponents()
    {
        if (runtime == null)
        {
            return;
        }

        if (monsterHealth != null)
        {
            monsterHealth.ConfigureStats(runtime.maxHP, runtime.currentDEF);
            monsterHealth.SetCurrentHP(runtime.currentHP);
            if (monsterHealth.animator == null)
            {
                monsterHealth.animator = monsterAnimator;
            }
            monsterHealth.hurtStateName = hurtStateName;
            monsterHealth.deadStateName = deadStateName;
        }

        if (monsterAttackController != null)
        {
            monsterAttackController.Bind(this);
        }

        if (navAgent != null)
        {
            ConfigureNavAgentForRuntime();
        }
    }

    private void ConfigureNavAgentForRuntime()
    {
        if (navAgent == null || runtime == null)
        {
            return;
        }

        navAgent.speed = runtime.moveSpeed;
        navAgent.stoppingDistance = Mathf.Max(0f, reachDistance);
        navAgent.autoBraking = true;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    private bool IsTargetPlayerPosition(Vector3 targetPosition)
    {
        if (targetPlayer == null)
        {
            return false;
        }

        return GetFlatDistance(targetPosition, targetPlayer.position) <= 0.25f;
    }

    private Vector3 GetPlayerApproachPosition(Vector3 playerPosition)
    {
        Vector3 awayFromPlayer = transform.position - playerPosition;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude <= 0.0001f)
        {
            awayFromPlayer = -transform.forward;
            awayFromPlayer.y = 0f;
        }

        return playerPosition + awayFromPlayer.normalized * GetPlayerApproachStopDistance();
    }

    private float GetPlayerApproachStopDistance()
    {
        if (runtime == null)
        {
            return Mathf.Max(0.05f, minPlayerSeparation);
        }

        float attackRange = Mathf.Max(0.05f, runtime.attackRange);
        float desired = Mathf.Max(minPlayerSeparation, attackRange * Mathf.Clamp01(playerStopRangeRatio));
        if (desired >= attackRange)
        {
            desired = Mathf.Max(0.05f, attackRange - Mathf.Max(0.01f, navStoppingDistancePadding));
        }

        return desired;
    }

    private static float GetFlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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

    private void InitializeStateMachine()
    {
        stateMachine = new MonsterStateMachine();
        stateMachine.RegisterState(new MonsterIdleState(this));
        stateMachine.RegisterState(new MonsterPatrolState(this));
        stateMachine.RegisterState(new MonsterTauntState(this));
        stateMachine.RegisterState(new MonsterChaseState(this));
        stateMachine.RegisterState(new MonsterAttackState(this));
        stateMachine.RegisterState(new MonsterHurtState(this));
        stateMachine.RegisterState(new MonsterReturnState(this));
        stateMachine.RegisterState(new MonsterDeadState(this));
        stateMachine.ChangeState(usePatrol ? MonsterStateType.Patrol : MonsterStateType.Idle);
        currentStateType = stateMachine.CurrentStateType;
    }

    private void HandleDead(MonsterHealth health)
    {
        if (runtime != null)
        {
            runtime.isDead = true;
            runtime.currentHP = 0;
        }

        // 死亡后停止寻路并禁用 Agent，避免死亡动画期间继续移动或被其他 Agent 推动
        StopMovement();
        if (navAgent != null && navAgent.enabled)
            navAgent.enabled = false;

        // 走事件总线广播：战斗侧只发出“某 ID 的怪死了”这个事实，由订阅方（任务系统）自行决定是否推进，
        // 战斗系统不再直接依赖 TaskManager，二者彻底解耦。
        GameMgr.Event?.Broadcast("MonsterKilled", new GameEventParameter<int>(monsterID));

        SpawnDropRewardsAsync().Forget();
        ChangeState(MonsterStateType.Dead);
    }

    private void HandlePlayerDied(PlayerHealth playerHealth)
    {
        if (playerHealth == null || IsDead())
        {
            return;
        }

        if (!IsSameTargetHierarchy(targetPlayer, playerHealth.transform))
        {
            return;
        }

        ClearTarget();
        ResetAttackTimer();
        StopMovement();

        if (currentStateType != MonsterStateType.Dead && currentStateType != MonsterStateType.Return)
        {
            ChangeState(MonsterStateType.Return);
        }
    }

    private async UniTaskVoid SpawnDropRewardsAsync()
    {
        if (hasGrantedDropRewards || runtime == null)
        {
            return;
        }

        hasGrantedDropRewards = true;
        await MonsterDropSpawner.SpawnDropsAsync(runtime, transform.position);
    }

    private void HandleHealthChanged(MonsterHealth health)
    {
        if (runtime == null || health == null)
        {
            return;
        }

        runtime.currentHP = health.currentHP;
        runtime.maxHP = health.maxHP;
        runtime.currentDEF = health.defense;
        runtime.isDead = health.IsDead;
    }

    private void HandleDamaged(MonsterHealth health, AttackDamageInfo damageInfo, int actualDamage)
    {
        if (IsDead())
        {
            return;
        }

        if (damageInfo.attacker != null)
        {
            SetTarget(damageInfo.attacker.transform);
        }
        else
        {
            CachePlayerTarget();
        }

        if (currentStateType == MonsterStateType.Dead)
        {
            return;
        }

        ChangeState(MonsterStateType.Hurt);
    }
}
