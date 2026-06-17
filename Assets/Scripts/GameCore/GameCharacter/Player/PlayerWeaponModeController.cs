using System.Collections;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerWeaponModeController : MonoBehaviour
{
    private enum WeaponMode
    {
        Armed,
        Unarmed,
        Equipping,
        Unequipping
    }

    [Header("Animator Controllers")]
    [SerializeField] private RuntimeAnimatorController armedAnimatorController;
    [SerializeField] private RuntimeAnimatorController unarmedAnimatorController;
    [SerializeField] private string armedControllerAddress = "PlayerAnimator";
    [SerializeField] private string unarmedControllerAddress = "PlayerNormal";

    [Header("State Names")]
    [SerializeField] private string armedIdleState = "Idle";
    [SerializeField] private string unarmedIdleState = "Idel";
    [SerializeField] private string equipState = "EquipWeapon";
    [SerializeField] private string unloadState = "UnloadWeapon";
    [SerializeField] private string buildActionState = "Build01";
    [SerializeField] private string weaponSwitchLayerName = "RightHand";
    [SerializeField] private string weaponSwitchDefaultState = "Empty";
    [SerializeField] private float transitionBlendDuration = 0.08f;
    [SerializeField] private float transitionCompleteNormalizedTime = 0.98f;
    [SerializeField] private float transitionMaxWaitTime = 3f;

    private PlayerStateDriver stateDriver;
    private PlayerContext ctx;
    private PlayerModelManager modelManager;
    private PlayerHealth playerHealth;
    private PlayerWeaponAttackEffectController attackEffectController;
    private Animator animator;
    private Coroutine transitionRoutine;
    private Coroutine buildActionRoutine;
    private WeaponMode mode = WeaponMode.Unarmed;
    private bool subscribedToEquipment;

    public bool IsArmed => mode == WeaponMode.Armed;
    public bool IsTransitioning => mode == WeaponMode.Equipping || mode == WeaponMode.Unequipping;
    public bool CanUseWeaponAttack => IsArmed && !IsTransitioning;
    public event Action<bool> OnArmedStateChanged;

    private void Awake()
    {
        stateDriver = GetComponent<PlayerStateDriver>();
        modelManager = GetComponent<PlayerModelManager>();
        playerHealth = GetComponent<PlayerHealth>();
        attackEffectController = GetComponent<PlayerWeaponAttackEffectController>();
    }

    private void OnEnable()
    {
        if (IsRemoteNetworkReplica())
        {
            enabled = false;
            return;
        }

        SubscribeEquipmentChanged();

        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
            modelManager.onModelSwitched += HandleModelSwitched;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
            playerHealth.OnDamaged += HandlePlayerDamaged;
        }

        LoadAnimatorControllersAsync().Forget();
        SyncModeWithEquipment(false);
    }

    private IEnumerator Start()
    {
        yield return null;
        SubscribeEquipmentChanged();
        SyncModeWithEquipment(false);
    }

    private void OnDisable()
    {
        UnsubscribeEquipmentChanged();

        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            ToggleWeaponMode();
        }

        ApplyContextWeaponState();
    }

    public void Initialize(PlayerContext playerContext, Animator targetAnimator)
    {
        ctx = playerContext;
        BindAnimator(targetAnimator);
        ApplyModeToContextAndAnimator(false);
        SyncModeWithEquipment(false);
    }

    public void BindAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;
        if (animator == null)
        {
            return;
        }

        if (armedAnimatorController == null && animator.runtimeAnimatorController != null)
        {
            armedAnimatorController = animator.runtimeAnimatorController;
        }

        if (armedAnimatorController == null && modelManager != null)
        {
            armedAnimatorController = modelManager.playerAnimatorController;
        }

        if (unarmedAnimatorController == null && modelManager != null)
        {
            unarmedAnimatorController = modelManager.playerNormalAnimatorController;
        }

        LoadAnimatorControllersFromProjectPath();
        ApplyModeToContextAndAnimator(false);
        SyncModeWithEquipment(false);
    }

    public void ToggleWeaponMode()
    {
        if (!HasActiveHandItem())
        {
            ForceUnarmedMode(false);
            return;
        }

        if (mode == WeaponMode.Armed)
        {
            BeginUnequip();
            return;
        }

        if (mode == WeaponMode.Unarmed)
        {
            BeginEquip(false);
        }
    }

    public void EquipCurrentWeapon()
    {
        if (mode == WeaponMode.Armed || mode == WeaponMode.Equipping)
        {
            return;
        }

        if (!HasActiveHandItem())
        {
            ForceUnarmedMode(false);
            return;
        }

        BeginEquip(false);
    }

    public void EnterUnarmedMode()
    {
        if (mode == WeaponMode.Unarmed || mode == WeaponMode.Unequipping)
        {
            return;
        }

        BeginUnequip();
    }

    public void PlayBuildAction()
    {
        if (buildActionRoutine != null)
        {
            StopCoroutine(buildActionRoutine);
            buildActionRoutine = null;
        }

        buildActionRoutine = StartCoroutine(PlayRightHandAction(buildActionState));
    }

    public void RequestAttackWithWeapon()
    {
        if (attackEffectController == null)
        {
            attackEffectController = GetComponent<PlayerWeaponAttackEffectController>();
        }

        if (CanUseWeaponAttack)
        {
            attackEffectController?.PlayCurrentEffect();
            return;
        }

        StopCurrentWeaponAttack();
    }

    public async UniTaskVoid ApplyNetworkVisualMode(bool armed, Animator targetAnimator)
    {
        if (stateDriver == null)
        {
            stateDriver = GetComponent<PlayerStateDriver>();
        }

        if (ctx == null && stateDriver != null)
        {
            ctx = stateDriver.ctx;
        }

        if (modelManager == null)
        {
            modelManager = GetComponent<PlayerModelManager>();
        }

        if (targetAnimator != null)
        {
            animator = targetAnimator;
        }
        else if (!HasUsableAnimator() &&
                 stateDriver != null &&
                 stateDriver.ctx != null &&
                 PlayerStateDriver.HasPlayableAnimator(stateDriver.ctx.anim))
        {
            animator = stateDriver.ctx.anim;
        }

        if (armedAnimatorController == null && modelManager != null)
        {
            armedAnimatorController = modelManager.playerAnimatorController;
        }

        if (unarmedAnimatorController == null && modelManager != null)
        {
            unarmedAnimatorController = modelManager.playerNormalAnimatorController;
        }

        LoadAnimatorControllersFromProjectPath();

        if (GameMgr.AssetLoader != null)
        {
            if (armedAnimatorController == null && !string.IsNullOrWhiteSpace(armedControllerAddress))
            {
                armedAnimatorController = await GameMgr.AssetLoader.LoadAsset<RuntimeAnimatorController>(armedControllerAddress);
            }

            if (unarmedAnimatorController == null && !string.IsNullOrWhiteSpace(unarmedControllerAddress))
            {
                unarmedAnimatorController = await GameMgr.AssetLoader.LoadAsset<RuntimeAnimatorController>(unarmedControllerAddress);
            }
        }

        if (this == null)
        {
            return;
        }

        mode = armed ? WeaponMode.Armed : WeaponMode.Unarmed;
        ApplyContextAnimationNames();
        ApplyContextWeaponState();
        ApplyController(armed ? armedAnimatorController : unarmedAnimatorController);
        ResetWeaponSwitchLayer();
        ApplyWeaponAttachment(armed);
    }

    private async UniTaskVoid LoadAnimatorControllersAsync()
    {
        if (modelManager != null)
        {
            if (armedAnimatorController == null)
            {
                armedAnimatorController = modelManager.playerAnimatorController;
            }

            if (unarmedAnimatorController == null)
            {
                unarmedAnimatorController = modelManager.playerNormalAnimatorController;
            }
        }

        LoadAnimatorControllersFromProjectPath();

        if (GameMgr.AssetLoader == null)
        {
            SyncModeWithEquipment(false);
            return;
        }

        if (armedAnimatorController == null && !string.IsNullOrWhiteSpace(armedControllerAddress))
        {
            armedAnimatorController = await GameMgr.AssetLoader.LoadAsset<RuntimeAnimatorController>(armedControllerAddress);
        }

        if (unarmedAnimatorController == null && !string.IsNullOrWhiteSpace(unarmedControllerAddress))
        {
            unarmedAnimatorController = await GameMgr.AssetLoader.LoadAsset<RuntimeAnimatorController>(unarmedControllerAddress);
        }

        ApplyModeToContextAndAnimator(false);
        SyncModeWithEquipment(false);
    }

    private void HandleModelSwitched(Animator targetAnimator)
    {
        BindAnimator(targetAnimator);
    }

    private void HandlePlayerDamaged(PlayerHealth _, AttackDamageInfo __)
    {
        if (!HasActiveHandItem())
        {
            ForceUnarmedMode(false);
            return;
        }

        if (mode == WeaponMode.Unarmed || mode == WeaponMode.Unequipping)
        {
            BeginEquip(false);
        }
    }

    private void BeginEquip(bool attackAfterEquip)
    {
        if (!HasActiveHandItem())
        {
            ForceUnarmedMode(false);
            return;
        }

        StartTransition(WeaponMode.Equipping, equipState, true, attackAfterEquip);
    }

    private void BeginUnequip()
    {
        StartTransition(WeaponMode.Unequipping, unloadState, false, false);
    }

    private void StartTransition(WeaponMode transitionMode, string stateName, bool endArmed, bool attackAfterEquip)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        mode = transitionMode;
        ApplyContextWeaponState();
        LoadAnimatorControllersFromProjectPath();
        transitionRoutine = StartCoroutine(PlayTransition(stateName, endArmed, attackAfterEquip));
    }

    private IEnumerator PlayTransition(string stateName, bool endArmed, bool attackAfterEquip)
    {
        ApplyController(endArmed ? unarmedAnimatorController : armedAnimatorController);

        if (!HasUsableAnimator())
        {
            CompleteTransition(endArmed, attackAfterEquip);
            yield break;
        }

        int layerIndex = GetWeaponSwitchLayerIndex();
        if (layerIndex < 0)
        {
            Debug.LogWarning($"[PlayerWeaponModeController] Animator layer not found: {weaponSwitchLayerName}", this);
            CompleteTransition(endArmed, attackAfterEquip);
            yield break;
        }

        if (!animator.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning($"[PlayerWeaponModeController] Animator state not found on {weaponSwitchLayerName}: {stateName}", this);
            CompleteTransition(endArmed, attackAfterEquip);
            yield break;
        }

        animator.SetLayerWeight(layerIndex, 1f);
        animator.CrossFade(stateName, transitionBlendDuration, layerIndex, 0f);
        yield return null;

        float elapsed = 0f;
        bool reachedTargetState = false;
        while (HasUsableAnimator())
        {
            if (!animator.IsInTransition(layerIndex))
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
                bool isTargetState = info.IsName(stateName) ||
                                     info.shortNameHash == Animator.StringToHash(stateName) ||
                                     info.fullPathHash == Animator.StringToHash(weaponSwitchLayerName + "." + stateName);
                reachedTargetState |= isTargetState;

                if (reachedTargetState && info.normalizedTime >= transitionCompleteNormalizedTime)
                {
                    break;
                }
            }

            elapsed += Time.deltaTime;
            if (elapsed >= transitionMaxWaitTime)
            {
                Debug.LogWarning($"[PlayerWeaponModeController] Timed out waiting for transition state: {stateName}", this);
                break;
            }

            yield return null;
        }

        CompleteTransition(endArmed, attackAfterEquip);
    }

    private void CompleteTransition(bool endArmed, bool attackAfterEquip)
    {
        transitionRoutine = null;
        if (endArmed && !HasActiveHandItem())
        {
            ForceUnarmedMode(true);
            return;
        }

        RuntimeAnimatorController targetController = endArmed ? armedAnimatorController : unarmedAnimatorController;
        if (targetController == null)
        {
            Debug.LogWarning(
                endArmed
                    ? "[PlayerWeaponModeController] Armed animator controller is missing."
                    : "[PlayerWeaponModeController] Unarmed animator controller is missing. Assign PlayerNormal on PlayerModelManager or register it in Addressables.",
                this);
        }

        mode = endArmed ? WeaponMode.Armed : WeaponMode.Unarmed;
        ApplyWeaponAttachment(endArmed);
        ResetWeaponSwitchLayer();
        ApplyModeToContextAndAnimator(true);
        OnArmedStateChanged?.Invoke(IsArmed);

        bool fireStillPressed = GameMgr.input != null && GameMgr.input.Data.Fire;
        if (attackAfterEquip && fireStillPressed && CanUseWeaponAttack)
        {
            attackEffectController?.PlayCurrentEffect();
        }
    }

    private void SubscribeEquipmentChanged()
    {
        if (subscribedToEquipment || GameMgr.Equipment == null)
        {
            return;
        }

        GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        GameMgr.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        subscribedToEquipment = true;
    }

    private void UnsubscribeEquipmentChanged()
    {
        if (!subscribedToEquipment || GameMgr.Equipment == null)
        {
            subscribedToEquipment = false;
            return;
        }

        GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        subscribedToEquipment = false;
    }

    private void HandleEquipmentChanged()
    {
        SyncModeWithEquipment(true);
    }

    private void SyncModeWithEquipment(bool replayLocomotion)
    {
        if (GameMgr.Equipment == null)
        {
            return;
        }

        if (HasActiveHandItem())
        {
            return;
        }

        ForceUnarmedMode(replayLocomotion);
    }

    private bool HasActiveHandItem()
    {
        return GameMgr.Equipment != null && GameMgr.Equipment.GetActiveHandItem() != null;
    }

    private void ForceUnarmedMode(bool replayLocomotion)
    {
        if (mode == WeaponMode.Unarmed)
        {
            StopCurrentWeaponAttack();
            ApplyWeaponAttachment(false);
            ResetWeaponSwitchLayer();
            ApplyModeToContextAndAnimator(replayLocomotion);
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (buildActionRoutine != null)
        {
            StopCoroutine(buildActionRoutine);
            buildActionRoutine = null;
        }

        mode = WeaponMode.Unarmed;
        StopCurrentWeaponAttack();
        ApplyWeaponAttachment(false);
        ResetWeaponSwitchLayer();
        ApplyModeToContextAndAnimator(replayLocomotion);
        OnArmedStateChanged?.Invoke(false);
    }

    private void ApplyModeToContextAndAnimator(bool replayLocomotion)
    {
        ApplyContextAnimationNames();
        ApplyContextWeaponState();
        ApplyController(mode == WeaponMode.Unarmed ? unarmedAnimatorController : armedAnimatorController);

        if (replayLocomotion)
        {
            ReplayCurrentLocomotion();
        }
    }

    private void ApplyController(RuntimeAnimatorController controller)
    {
        if (animator == null || controller == null || animator.runtimeAnimatorController == controller)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
    }

    private void ApplyContextAnimationNames()
    {
        if (ctx == null)
        {
            return;
        }

        bool unarmed = mode == WeaponMode.Unarmed || mode == WeaponMode.Equipping;
        ctx.idleAnimStateName = unarmed ? unarmedIdleState : armedIdleState;
        ctx.walkAnimStateName = "Walk";
        ctx.runAnimStateName = "Run";
        ctx.jumpAnimStateName = "Jump";
        ctx.fallAnimStateName = "Fall";
        ctx.landAnimStateName = "Land";
        ctx.attackAnimStateName = "ATKTree";
    }

    private void ApplyContextWeaponState()
    {
        if (ctx != null)
        {
            ctx.weaponAttackEnabled = CanUseWeaponAttack;
        }
    }

    private void StopCurrentWeaponAttack()
    {
        if (attackEffectController == null)
        {
            attackEffectController = GetComponent<PlayerWeaponAttackEffectController>();
        }

        attackEffectController?.Stop();
    }

    private void ReplayCurrentLocomotion()
    {
        if (ctx == null || !HasUsableAnimator())
        {
            return;
        }

        string stateName;
        float blendDuration;
        if (!ctx.grounded)
        {
            if (ctx.isFlying || ctx.isGliding)
            {
                stateName = ctx.flyAnimStateName;
                blendDuration = ctx.airborneBlendDuration;
            }
            else
            {
                stateName = ctx.ySpeed >= 0f ? ctx.jumpAnimStateName : ctx.fallAnimStateName;
                blendDuration = ctx.airborneBlendDuration;
            }
        }
        else if (Mathf.Abs(ctx.move.x) > 0.01f || Mathf.Abs(ctx.move.z) > 0.01f)
        {
            stateName = GameMgr.input != null && GameMgr.input.Data.RunInput ? ctx.runAnimStateName : ctx.walkAnimStateName;
            blendDuration = ctx.locomotionBlendDuration;
        }
        else
        {
            stateName = ctx.idleAnimStateName;
            blendDuration = ctx.locomotionBlendDuration;
        }

        animator.CrossFade(stateName, blendDuration, 0);
    }

    private int GetWeaponSwitchLayerIndex()
    {
        return HasUsableAnimator() ? animator.GetLayerIndex(weaponSwitchLayerName) : -1;
    }

    private void ResetWeaponSwitchLayer()
    {
        int layerIndex = GetWeaponSwitchLayerIndex();
        if (layerIndex < 0 || animator == null)
        {
            return;
        }

        if (animator.HasState(layerIndex, Animator.StringToHash(weaponSwitchDefaultState)))
        {
            animator.Play(weaponSwitchDefaultState, layerIndex, 0f);
        }

        animator.SetLayerWeight(layerIndex, 0f);
    }

    private IEnumerator PlayRightHandAction(string stateName)
    {
        if (!HasUsableAnimator() || string.IsNullOrWhiteSpace(stateName))
        {
            buildActionRoutine = null;
            yield break;
        }

        int layerIndex = GetWeaponSwitchLayerIndex();
        if (layerIndex < 0)
        {
            Debug.LogWarning($"[PlayerWeaponModeController] Animator layer not found: {weaponSwitchLayerName}", this);
            buildActionRoutine = null;
            yield break;
        }

        if (!animator.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning($"[PlayerWeaponModeController] Animator state not found on {weaponSwitchLayerName}: {stateName}", this);
            buildActionRoutine = null;
            yield break;
        }

        animator.SetLayerWeight(layerIndex, 1f);
        animator.CrossFade(stateName, transitionBlendDuration, layerIndex, 0f);
        yield return null;

        float elapsed = 0f;
        bool reachedTargetState = false;
        while (HasUsableAnimator())
        {
            if (!animator.IsInTransition(layerIndex))
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
                bool isTargetState = info.IsName(stateName) ||
                                     info.shortNameHash == Animator.StringToHash(stateName) ||
                                     info.fullPathHash == Animator.StringToHash(weaponSwitchLayerName + "." + stateName);
                reachedTargetState |= isTargetState;

                if (reachedTargetState && info.normalizedTime >= transitionCompleteNormalizedTime)
                {
                    break;
                }
            }

            elapsed += Time.deltaTime;
            if (elapsed >= transitionMaxWaitTime)
            {
                Debug.LogWarning($"[PlayerWeaponModeController] Timed out waiting for right-hand state: {stateName}", this);
                break;
            }

            yield return null;
        }

        ResetWeaponSwitchLayer();
        buildActionRoutine = null;
    }

    private void ApplyWeaponAttachment(bool attachToHand)
    {
        PlayerWeaponAttachmentController attachmentController = GetComponentInChildren<PlayerWeaponAttachmentController>(true);
        if (attachmentController == null)
        {
            return;
        }

        if (attachToHand)
        {
            attachmentController.AttachWeaponToHand();
        }
        else
        {
            attachmentController.AttachWeaponToBack();
        }
    }

    private void LoadAnimatorControllersFromProjectPath()
    {
#if UNITY_EDITOR
        if (armedAnimatorController == null)
        {
            armedAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/System/AnumationSystem/Animator/PlayerAnimator.controller");
        }

        if (unarmedAnimatorController == null)
        {
            unarmedAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/System/AnumationSystem/Animator/PlayerNormal.controller");
        }
#endif
    }

    private bool HasUsableAnimator()
    {
        return PlayerStateDriver.HasPlayableAnimator(animator);
    }

    private bool IsRemoteNetworkReplica()
    {
        Unity.Netcode.NetworkObject networkObject = GetComponentInParent<Unity.Netcode.NetworkObject>();
        return networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner;
    }
}
