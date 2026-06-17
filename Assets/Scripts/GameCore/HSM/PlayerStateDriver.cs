using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerStateDriver : MonoBehaviour
{
    private static readonly Dictionary<RuntimeAnimatorController, Dictionary<int, AnimatorControllerParameterType>> AnimatorParameterCache =
        new Dictionary<RuntimeAnimatorController, Dictionary<int, AnimatorControllerParameterType>>();

    public PlayerContext ctx = new PlayerContext();
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundMask;
    public float groundedBufferTime = 0.12f;
    public bool drawGizmos = true;
    public bool logStateChanges = false;
    public float rotSmoothTime = 0.05f;
    public CinemachineVirtualCamera virtualCamera;
    public Animator wingAnimator;
    public string wingNameHint = "Wing";

    [Header("Hit Reaction")]
    [SerializeField, Min(0.01f)] private float hitReactionDuration = 0.32f;
    [SerializeField, Min(0f)] private float hitReactionHorizontalDamping = 5f;

    string lastPath;
    float targetRot = 0.0f;
    float rotVelocity;
    Vector3 moveDir;
    float groundedBufferTimer;
    bool lastJumpInput;
    string currentWingState = string.Empty;
    float hitReactionTimer;
    bool hitReactionMovementActive;
    bool warnedMissingHitState;

    private GameObject mainCamera;
    private CharacterController cC;
    private StateMachine machine;
    private State root;

    public bool IsLocalControlEnabled { get; private set; } = true;
    public bool IsLocalGameplayPlayer => ShouldRegisterAsLocalPlayer();

    public void ApplyKnockback(Vector3 direction, float horizontalSpeed, float verticalSpeed)
    {
        if (!IsLocalControlEnabled)
        {
            return;
        }

        Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
        if (horizontal.sqrMagnitude <= 0.0001f)
        {
            horizontal = -transform.forward;
        }

        horizontal.Normalize();
        ctx.airVelocity = horizontal * Mathf.Max(0f, horizontalSpeed);
        ctx.ySpeed = Mathf.Max(ctx.ySpeed, Mathf.Max(0f, verticalSpeed));
        ctx.grounded = false;
        groundedBufferTimer = 0f;
    }

    public void ApplyHitReaction(Vector3 direction, float horizontalSpeed, float verticalSpeed)
    {
        ApplyKnockback(direction, horizontalSpeed, verticalSpeed);

        if (!IsLocalControlEnabled)
        {
            return;
        }

        hitReactionTimer = Mathf.Max(0.01f, hitReactionDuration);
        hitReactionMovementActive = true;
        ctx.jumpPressed = false;
        ctx.isLeftPressed = false;
        ctx.weaponAttackPlaying = false;
        PlayHitReactionAnimation();
    }

    public void SetLocalControlEnabled(bool enabled)
    {
        IsLocalControlEnabled = enabled;

        if (!enabled)
        {
            ctx.move = Vector3.zero;
            ctx.look = Vector2.zero;
            ctx.jumpPressed = false;
            ctx.jumpHeld = false;
            ctx.isLeftPressed = false;
            ctx.isRightPressed = false;
            ctx.rootMotionDelta = Vector3.zero;
            ctx.airVelocity = Vector3.zero;
        }
    }

    void Start()
    {
        bool isLocalGameplayPlayer = IsLocalGameplayPlayer;
        InitializePosition();
        cC = gameObject.GetComponent<CharacterController>();
        ctx.cc = cC;
        if (isLocalGameplayPlayer && GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
        }

        if (isLocalGameplayPlayer)
        {
            if (GetComponent<PlayerDeathController>() == null)
            {
                gameObject.AddComponent<PlayerDeathController>();
            }

            BuffComponent buffComponent = GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                buffComponent = gameObject.AddComponent<BuffComponent>();
            }

            buffComponent.Initialize(new PlayerBuffTarget(GetComponent<PlayerHealth>()));
            ApplyInitialBuffs(buffComponent);
            GameMgr.Equipment?.HandleEquipmentInventoryChanged();
        }

        if (GetComponent<PlayerWeaponModelController>() == null)
        {
            gameObject.AddComponent<PlayerWeaponModelController>();
        }

        if (GetComponent<PlayerWingModelController>() == null)
        {
            gameObject.AddComponent<PlayerWingModelController>();
        }

        if (GetComponent<PlayerWeaponTrailController>() == null)
        {
            gameObject.AddComponent<PlayerWeaponTrailController>();
        }

        if (GetComponent<PlayerWeaponAttackEffectController>() == null)
        {
            gameObject.AddComponent<PlayerWeaponAttackEffectController>();
        }

        if (GetComponent<PlayerAudioController>() == null)
        {
            gameObject.AddComponent<PlayerAudioController>();
        }

        if (GetComponent<PlayerWeaponModeController>() == null)
        {
            gameObject.AddComponent<PlayerWeaponModeController>();
        }

        BindAnimator(ResolveGameplayAnimator());
        ResolveWingAnimator();
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        PlayerModelManager modelManager = GetComponent<PlayerModelManager>();
        if (modelManager != null)
        {
            modelManager.onModelSwitched += BindAnimator;
        }

        ctx.useLegacyAttackLayer = false;
        InitializeFlightEnergy();

        root = new PlayerRoot(null, ctx);
        var builder = new StateMachineBuilder(root);
        machine = builder.Build();

        if (groundCheck == null)
        {
            var t = new GameObject("groundCheck").transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            groundCheck = t;
        }
    }

    void Update()
    {
        if (!IsLocalControlEnabled)
        {
            return;
        }

        bool jumpInput = GameMgr.input.Data.JumpInput;
        ctx.jumpPressed = jumpInput && !lastJumpInput;
        ctx.jumpHeld = jumpInput;
        lastJumpInput = jumpInput;
        ctx.move.x = GameMgr.input.Data.DirKeyAxis.x;
        ctx.move.z = GameMgr.input.Data.DirKeyAxis.y;
        ctx.look = GameMgr.input.Data.Look;
        ctx.isLeftPressed = GameMgr.input.Data.Fire;
        ctx.isRightPressed = GameMgr.input.Data.RightPress;
        UpdateWorldMoveDirection();

        bool isGroundedNow = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);
        if (!isGroundedNow)
        {
            isGroundedNow = Physics.CheckSphere(groundCheck.position + Vector3.down * 0.1f, groundRadius, groundMask);
        }

        isGroundedNow |= cC != null && cC.isGrounded;

        if (ctx.ySpeed > 0f)
        {
            isGroundedNow = false;
            groundedBufferTimer = 0f;
        }

        if (isGroundedNow)
        {
            groundedBufferTimer = groundedBufferTime;
        }
        else
        {
            groundedBufferTimer -= Time.deltaTime;
        }

        ctx.grounded = isGroundedNow || groundedBufferTimer > 0f;
        bool isHitReacting = UpdateHitReaction(Time.deltaTime);
        if (!isHitReacting)
        {
            machine.Tick(Time.deltaTime);
        }

        UpdateAnimatorParameters();
        UpdateAnimatorRuntimeSpeed();
        UpdateFlightVisuals();

        var path = StatePath(machine.Root.Leaf());
        if (logStateChanges && path != lastPath)
        {
            Debug.Log("State:" + path);
        }
        lastPath = path;

        HandleMoveMent();
        if (!isHitReacting && (ctx.move.x != 0 || ctx.move.z != 0 || ctx.isLeftPressed))
        {
            HandleRotation();
        }
        Aim();
    }

    private void OnDisable()
    {
        if (ctx.anim != null)
        {
            ctx.anim.speed = 1f;
        }

        ctx.currentAnimatorSpeed = 1f;
        ctx.weaponAttackPlaying = false;
        ctx.isFlying = false;
        ctx.isGliding = false;
        ResetFlightVisuals();
    }

    private void InitializePosition()
    {
        if (ShouldRegisterAsLocalPlayer())
        {
            GameMgr.Instance.Player = this;
        }
    }

    private bool ShouldRegisterAsLocalPlayer()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }

    private void ApplyInitialBuffs(BuffComponent buffComponent)
    {
        if (buffComponent == null) return;

        PlayerInitialDataSO initial = GameMgr.Instance != null ? GameMgr.Instance.playerInitialData : null;
        if (initial == null || initial.initialBuffIDs == null || initial.initialBuffIDs.Length == 0) return;

        for (int i = 0; i < initial.initialBuffIDs.Length; i++)
        {
            int id = initial.initialBuffIDs[i];
            if (id <= 0) continue;

            BuffData data = GameMgr.Buff != null ? GameMgr.Buff.GetBuffData(id) : null;
            if (data == null)
            {
                Debug.LogWarning($"[PlayerStateDriver] Initial buff data not found: id={id}");
                continue;
            }
            buffComponent.Apply(data, gameObject);
        }
    }

    private void HandleMoveMent()
    {
        // 横向位移先算进 movement，最后把垂直位移并入同一个向量，
        // 整帧只调用一次 cC.Move，避免两次独立碰撞解算在平地/接缝处造成垂直微抖。
        Vector3 movement;
        if (hitReactionMovementActive)
        {
            ctx.rootMotionDelta = Vector3.zero;
            movement = ctx.airVelocity * Time.deltaTime;
        }
        else if (ctx.grounded && ctx.anim != null)
        {
            bool isLandingState = machine != null && machine.Root.Leaf() is LandingState;
            Vector3 rootMotion = ctx.rootMotionDelta;
            rootMotion.y = 0f;
            movement = rootMotion;
            if (isLandingState)
            {
                movement += ctx.landingCarryVelocity * Time.deltaTime;
                ctx.landingCarryVelocity = Vector3.MoveTowards(
                    ctx.landingCarryVelocity,
                    Vector3.zero,
                    ctx.landingCarryDeceleration * Time.deltaTime);
            }
            else
            {
                ctx.landingCarryVelocity = Vector3.zero;
            }

            ctx.rootMotionDelta = Vector3.zero;
        }
        else
        {
            ctx.rootMotionDelta = Vector3.zero;
            movement = ctx.airVelocity * Time.deltaTime;
        }

        movement.y += ctx.ySpeed * Time.deltaTime;
        cC.Move(movement);
    }

    private bool UpdateHitReaction(float deltaTime)
    {
        if (hitReactionTimer <= 0f)
        {
            hitReactionMovementActive = false;
            return false;
        }

        hitReactionMovementActive = true;
        hitReactionTimer = Mathf.Max(0f, hitReactionTimer - Mathf.Max(0f, deltaTime));

        if (!ctx.grounded || ctx.ySpeed > 0f)
        {
            ctx.ySpeed += ctx.fallSpeed * deltaTime;
        }

        ctx.airVelocity = Vector3.MoveTowards(
            ctx.airVelocity,
            Vector3.zero,
            Mathf.Max(0f, hitReactionHorizontalDamping) * deltaTime);

        return true;
    }

    private void PlayHitReactionAnimation()
    {
        if (!HasPlayableAnimator(ctx.anim))
        {
            return;
        }

        string stateName = string.IsNullOrWhiteSpace(ctx.hitAnimStateName) ? "Hit" : ctx.hitAnimStateName;
        int layerIndex = 0;
        if (!ctx.anim.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            if (!warnedMissingHitState)
            {
                Debug.LogWarning($"[PlayerStateDriver] Animator state not found: {stateName}", this);
                warnedMissingHitState = true;
            }

            return;
        }

        ctx.anim.CrossFade(stateName, Mathf.Max(0f, ctx.hitReactionBlendDuration), layerIndex, 0f);
    }

    private void UpdateWorldMoveDirection()
    {
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        moveDir = (cameraForward * ctx.move.z + cameraRight * ctx.move.x).normalized;
        ctx.worldMoveDir = moveDir;
    }

    private void HandleRotation()
    {
        Vector3 inputDir = new Vector3(ctx.move.x, 0.0f, ctx.move.z).normalized;
        float rotation;
        if (ctx.isLeftPressed)
        {
            targetRot = mainCamera.transform.eulerAngles.y;
            rotation = targetRot;
        }
        else
        {
            targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRot, ref rotVelocity, rotSmoothTime);
        }

        transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
    }

    private void Aim()
    {
        if (ctx.isRightPressed)
        {
            virtualCamera.Priority = ctx.priority;
        }
        else
        {
            virtualCamera.Priority = ctx.priority + 2;
        }
    }

    private void BindAnimator(Animator animator)
    {
        if (!HasPlayableAnimator(animator))
        {
            animator = ResolveGameplayAnimator();
        }

        ctx.anim = animator;
        warnedMissingHitState = false;
        if (animator == null)
        {
            return;
        }

        PlayerRootMotionRelay relay = animator.GetComponent<PlayerRootMotionRelay>();
        if (relay != null)
        {
            relay.Bind(this);
        }

        PlayerWeaponModeController weaponModeController = GetComponent<PlayerWeaponModeController>();
        if (weaponModeController != null)
        {
            weaponModeController.Initialize(ctx, animator);
        }

        animator.speed = Mathf.Clamp(ctx.currentAnimatorSpeed, 0.1f, 5f);
    }

    private void InitializeFlightEnergy()
    {
        ctx.maxFlightEnergy = Mathf.Max(0f, ctx.maxFlightEnergy);
        if (ctx.currentFlightEnergy <= 0f || ctx.currentFlightEnergy > ctx.maxFlightEnergy)
        {
            ctx.currentFlightEnergy = ctx.maxFlightEnergy;
        }
    }

    private void UpdateAnimatorParameters()
    {
        if (ctx.anim == null)
        {
            return;
        }

        SetAnimatorFloatIfExists(ctx.anim, "DirX", ctx.move.x);
        SetAnimatorFloatIfExists(ctx.anim, "DirZ", ctx.move.z);
    }

    private void UpdateAnimatorRuntimeSpeed()
    {
        if (!HasPlayableAnimator(ctx.anim))
        {
            return;
        }

        float targetSpeed = 1f;
        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data != null)
        {
            if (ctx.weaponAttackPlaying || ctx.isLeftPressed)
            {
                targetSpeed = data.GetAttackSpeedMultiplier();
            }
            else if (ctx.isMoving || ctx.isFlying || ctx.isGliding)
            {
                targetSpeed = data.GetMoveSpeedMultiplier();
            }
        }

        targetSpeed = Mathf.Clamp(targetSpeed, 0.1f, 5f);
        if (!Mathf.Approximately(ctx.currentAnimatorSpeed, targetSpeed))
        {
            ctx.currentAnimatorSpeed = targetSpeed;
            ctx.anim.speed = targetSpeed;
        }
    }

    public static void SetAnimatorFloatIfExists(Animator animator, string parameterName, float value)
    {
        if (!HasPlayableAnimator(animator) || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        int parameterHash = Animator.StringToHash(parameterName);
        if (!HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Float))
        {
            return;
        }

        animator.SetFloat(parameterHash, value);
    }

    private static bool HasAnimatorParameter(
        Animator animator,
        int parameterHash,
        AnimatorControllerParameterType expectedType)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            return false;
        }

        if (!AnimatorParameterCache.TryGetValue(controller, out Dictionary<int, AnimatorControllerParameterType> parameters))
        {
            parameters = new Dictionary<int, AnimatorControllerParameterType>();
            AnimatorControllerParameter[] animatorParameters = animator.parameters;
            for (int i = 0; i < animatorParameters.Length; i++)
            {
                AnimatorControllerParameter parameter = animatorParameters[i];
                parameters[parameter.nameHash] = parameter.type;
            }

            AnimatorParameterCache[controller] = parameters;
        }

        return parameters.TryGetValue(parameterHash, out AnimatorControllerParameterType type) && type == expectedType;
    }

    public static bool HasPlayableAnimator(Animator animator)
    {
        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
    }

    private Animator ResolveGameplayAnimator()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate != null && candidate.runtimeAnimatorController != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void UpdateFlightVisuals()
    {
        if (ctx.isFlying)
        {
            PlayWingState(ctx.wingFlyAnimStateName);
            return;
        }

        if (ctx.isGliding)
        {
            PlayWingState(ctx.wingOpenAnimStateName);
            return;
        }

        PlayWingState(ctx.wingCloseAnimStateName);
    }

    private void ResetFlightVisuals()
    {
        PlayWingState(ctx.wingCloseAnimStateName);
    }

    private void PlayWingState(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        if (wingAnimator == null)
        {
            ResolveWingAnimator();
        }

        if (wingAnimator == null || string.Equals(currentWingState, stateName, System.StringComparison.Ordinal))
        {
            return;
        }

        if (PlayAnimatorState(wingAnimator, stateName, 0, ctx.airborneBlendDuration))
        {
            currentWingState = stateName;
        }
    }

    private void ResolveWingAnimator()
    {
        if (wingAnimator != null)
        {
            return;
        }

        Transform hinted = FindChildByName(transform, wingNameHint);
        if (hinted != null)
        {
            wingAnimator = hinted.GetComponentInChildren<Animator>(true);
            if (wingAnimator != null && wingAnimator != ctx.anim)
            {
                return;
            }
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null || candidate == ctx.anim)
            {
                continue;
            }

            string candidateName = candidate.name;
            if (!string.IsNullOrWhiteSpace(candidateName) &&
                candidateName.IndexOf(wingNameHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wingAnimator = candidate;
                return;
            }
        }
    }

    private static Transform FindChildByName(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrWhiteSpace(namePart))
        {
            return null;
        }

        if (root.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool PlayAnimatorState(Animator animator, string stateName, int layerIndex, float blendDuration)
    {
        if (!HasPlayableAnimator(animator) || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        if (!animator.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            return false;
        }

        animator.CrossFade(stateName, blendDuration, layerIndex, 0f);
        return true;
    }

    public void ReceiveRootMotion(Vector3 deltaPosition)
    {
        if (!ctx.grounded)
        {
            return;
        }

        ctx.rootMotionDelta += deltaPosition;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }

    static string StatePath(State s)
    {
        return string.Join(">", s.PathToRoot().AsEnumerable().Reverse().Select(n => n.GetType().Name));
    }
}
