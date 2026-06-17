using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private float ownerSyncInterval = 0.05f;
    [SerializeField] private float remotePositionLerp = 18f;
    [SerializeField] private float remoteRotationLerp = 18f;

    private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector2> networkMove = new NetworkVariable<Vector2>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> networkGrounded = new NetworkVariable<bool>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> networkAnimState = new NetworkVariable<int>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> networkCurrentHP = new NetworkVariable<int>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> networkMaxHP = new NetworkVariable<int>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString64Bytes> networkDisplayName = new NetworkVariable<FixedString64Bytes>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString128Bytes> networkWeaponModelName = new NetworkVariable<FixedString128Bytes>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> networkWeaponArmed = new NetworkVariable<bool>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> networkWeaponIsArtifact = new NetworkVariable<bool>(
        writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> networkWeaponIsTool = new NetworkVariable<bool>(
        writePerm: NetworkVariableWritePermission.Server);

    private PlayerStateDriver playerDriver;
    private CharacterController characterController;
    private Animator animator;
    private PlayerWeaponModelController weaponModelController;
    private PlayerWeaponAttackEffectController weaponAttackEffectController;
    private PlayerHealth boundOwnerHealth;
    private float nextSyncTime;
    private int lastRemoteAnimState = -1;
    private Animator lastRemoteAnimator;
    private RuntimeAnimatorController lastRemoteAnimatorController;
    private bool hasAppliedRemoteWeaponVisual;
    private string lastAppliedRemoteWeaponModelName = string.Empty;
    private bool lastAppliedRemoteWeaponArmed;
    private bool lastAppliedRemoteWeaponIsArtifact;
    private bool lastAppliedRemoteWeaponIsTool;
    private static readonly List<ulong> SingleClientTarget = new List<ulong>(1);

    public PlayerStateDriver PlayerDriver => playerDriver;
    public bool IsLocalOwner => IsSpawned && IsOwner;
    public string DisplayName => networkDisplayName.Value.ToString();
    public int CurrentHP => networkCurrentHP.Value;
    public int MaxHP => networkMaxHP.Value;
    public float NormalizedHP => networkMaxHP.Value <= 0 ? 0f : (float)networkCurrentHP.Value / networkMaxHP.Value;

    private void Awake()
    {
        ResolveComponents();
        DisablePlayerInputsUntilOwnershipKnown();
    }

    public override void OnNetworkSpawn()
    {
        ResolveComponents();
        ApplyOwnershipState();

        if (IsServer)
        {
            Transform target = ResolveControlledTransform();
            networkPosition.Value = target.position;
            networkRotation.Value = target.rotation;

            if (IsOwner)
            {
                WriteOwnerVitals();
                WriteOwnerWeaponVisual();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        UnbindOwnerHealth();

        if (GameMgr.Instance != null && GameMgr.Instance.Player == playerDriver)
        {
            GameMgr.Instance.Player = null;
        }
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsOwner)
        {
            SyncOwnerState();
            return;
        }

        ApplyRemoteState();
    }

    private void ResolveComponents()
    {
        if (playerDriver == null)
        {
            playerDriver = GetComponent<PlayerStateDriver>() ?? GetComponentInChildren<PlayerStateDriver>(true);
        }

        if (characterController == null && playerDriver != null)
        {
            characterController = playerDriver.GetComponent<CharacterController>();
        }

        if (animator == null && playerDriver != null)
        {
            animator = ResolveAnimator();
        }

        if (weaponModelController == null)
        {
            weaponModelController = GetComponent<PlayerWeaponModelController>() ??
                                    GetComponentInChildren<PlayerWeaponModelController>(true);
        }

        if (weaponAttackEffectController == null)
        {
            weaponAttackEffectController = GetComponent<PlayerWeaponAttackEffectController>() ??
                                           GetComponentInChildren<PlayerWeaponAttackEffectController>(true);
        }
    }

    private void ApplyOwnershipState()
    {
        bool isLocalOwner = IsOwner;

        if (playerDriver != null)
        {
            playerDriver.SetLocalControlEnabled(isLocalOwner);
            playerDriver.enabled = true;

            if (isLocalOwner && GameMgr.Instance != null)
            {
                GameMgr.Instance.Player = playerDriver;
                InitializeLocalGameplayState();
                RebindSceneCamerasToLocalPlayer();
            }
        }

        if (characterController != null)
        {
            characterController.enabled = isLocalOwner || IsServer;
        }

        ApplyGameplayOwnershipState(isLocalOwner);
        ApplyCameraOwnershipState(isLocalOwner);
    }

    private void SyncOwnerState()
    {
        Transform target = ResolveControlledTransform();

        if (IsServer)
        {
            WriteNetworkState(target.position, target.rotation);
            return;
        }

        if (Time.unscaledTime < nextSyncTime)
        {
            return;
        }

        nextSyncTime = Time.unscaledTime + ownerSyncInterval;
        SubmitOwnerStateServerRpc(
            target.position,
            target.rotation,
            new Vector2(playerDriver.ctx.move.x, playerDriver.ctx.move.z),
            playerDriver.ctx.grounded,
            ResolveOwnerAnimState(),
            ResolveOwnerCurrentHP(),
            ResolveOwnerMaxHP(),
            ResolveOwnerDisplayName(),
            ResolveOwnerWeaponModelName(),
            ResolveOwnerWeaponArmed(),
            ResolveOwnerWeaponIsArtifact(),
            ResolveOwnerWeaponIsTool());
    }

    private void ApplyRemoteState()
    {
        Transform target = ResolveControlledTransform();
        target.position = Vector3.Lerp(
            target.position,
            networkPosition.Value,
            remotePositionLerp * Time.deltaTime);
        target.rotation = Quaternion.Slerp(
            target.rotation,
            networkRotation.Value,
            remoteRotationLerp * Time.deltaTime);

        ApplyRemoteWeaponVisualIfChanged();
        PlayRemoteAnimState();
        if (animator == null)
        {
            return;
        }

        PlayerStateDriver.SetAnimatorFloatIfExists(animator, "DirX", networkMove.Value.x);
        PlayerStateDriver.SetAnimatorFloatIfExists(animator, "DirZ", networkMove.Value.y);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitOwnerStateServerRpc(
        Vector3 position,
        Quaternion rotation,
        Vector2 move,
        bool grounded,
        int animState,
        int currentHP,
        int maxHP,
        FixedString64Bytes displayName,
        FixedString128Bytes weaponModelName,
        bool weaponArmed,
        bool weaponIsArtifact,
        bool weaponIsTool,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        networkMove.Value = move;
        networkGrounded.Value = grounded;
        networkAnimState.Value = animState;
        networkPosition.Value = position;
        networkRotation.Value = rotation;
        networkCurrentHP.Value = Mathf.Clamp(currentHP, 0, Mathf.Max(1, maxHP));
        networkMaxHP.Value = Mathf.Max(1, maxHP);
        networkDisplayName.Value = displayName;
        WriteWeaponVisualValues(weaponModelName, weaponArmed, weaponIsArtifact, weaponIsTool);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitOwnerVitalsServerRpc(
        int currentHP,
        int maxHP,
        FixedString64Bytes displayName,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        WriteVitalsValues(currentHP, maxHP, displayName);
    }

    public void SubmitBossDamage(string bossId, AttackDamageInfo damageInfo)
    {
        if (!IsSpawned || !IsOwner || IsServer || string.IsNullOrWhiteSpace(bossId))
        {
            return;
        }

        SubmitBossDamageServerRpc(
            bossId,
            Mathf.Max(1, damageInfo.damage),
            damageInfo.hitPoint,
            damageInfo.hitDirection);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitBossDamageServerRpc(
        FixedString128Bytes bossId,
        int damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        int actualDamage = NetworkBoss.ApplyServerBossDamage(
            bossId.ToString(),
            new AttackDamageInfo(Mathf.Max(1, damage), hitPoint, hitDirection, gameObject),
            out Vector3 popupPosition);
        if (actualDamage <= 0)
        {
            return;
        }

        SingleClientTarget.Clear();
        SingleClientTarget.Add(rpcParams.Receive.SenderClientId);
        ShowBossDamageNumberClientRpc(
            actualDamage,
            popupPosition,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = SingleClientTarget
                }
            });
    }

    public bool TryApplyBossDamageToOwnerFromServer(int damage, Vector3 hitPoint, Vector3 hitDirection, GameObject attacker)
    {
        if (!IsSpawned ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            OwnerClientId == NetworkManager.ServerClientId)
        {
            return false;
        }

        SingleClientTarget.Clear();
        SingleClientTarget.Add(OwnerClientId);
        ApplyBossDamageClientRpc(
            Mathf.Max(1, damage),
            hitPoint,
            hitDirection,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = SingleClientTarget
                }
            });
        return true;
    }

    [ClientRpc]
    private void ApplyBossDamageClientRpc(
        int damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner || playerDriver == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerDriver.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = playerDriver.gameObject.AddComponent<PlayerHealth>();
        }

        playerHealth.TakeDamage(new AttackDamageInfo(Mathf.Max(1, damage), hitPoint, hitDirection, null));
    }

    [ClientRpc]
    private void ShowBossDamageNumberClientRpc(
        int damage,
        Vector3 popupPosition,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        DamageNumberSpawner.Show(Mathf.Max(1, damage), popupPosition);
    }

    public void NotifyLocalWeaponAttackEffectStarted()
    {
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        FixedString128Bytes effectAddressKey = ResolveOwnerWeaponAttackEffectAddressKey();
        FixedString128Bytes effectWeaponName = ResolveOwnerWeaponAttackEffectWeaponName();
        FixedString128Bytes weaponModelName = ResolveOwnerWeaponModelName();
        bool weaponArmed = ResolveOwnerWeaponArmed();
        if (IsServer)
        {
            PlayWeaponAttackEffectClientRpc(effectAddressKey, effectWeaponName, weaponModelName, weaponArmed);
            return;
        }

        SubmitWeaponAttackEffectServerRpc(effectAddressKey, effectWeaponName, weaponModelName, weaponArmed);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitWeaponAttackEffectServerRpc(
        FixedString128Bytes effectAddressKey,
        FixedString128Bytes effectWeaponName,
        FixedString128Bytes weaponModelName,
        bool weaponArmed,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        PlayWeaponAttackEffectClientRpc(effectAddressKey, effectWeaponName, weaponModelName, weaponArmed);
    }

    [ClientRpc]
    private void PlayWeaponAttackEffectClientRpc(
        FixedString128Bytes effectAddressKey,
        FixedString128Bytes effectWeaponName,
        FixedString128Bytes weaponModelName,
        bool weaponArmed)
    {
        if (IsOwner)
        {
            return;
        }

        PlayRemoteWeaponAttackEffect(
            effectAddressKey.ToString(),
            effectWeaponName.ToString(),
            weaponModelName.ToString(),
            weaponArmed);
    }

    private void WriteNetworkState(Vector3 position, Quaternion rotation)
    {
        networkPosition.Value = position;
        networkRotation.Value = rotation;

        if (playerDriver != null)
        {
            networkMove.Value = new Vector2(playerDriver.ctx.move.x, playerDriver.ctx.move.z);
            networkGrounded.Value = playerDriver.ctx.grounded;
            networkAnimState.Value = ResolveOwnerAnimState();
            WriteOwnerVitals();
            WriteOwnerWeaponVisual();
        }
    }

    private void WriteOwnerVitals()
    {
        int maxHP = ResolveOwnerMaxHP();
        WriteVitalsValues(ResolveOwnerCurrentHP(), maxHP, ResolveOwnerDisplayName());
    }

    private void WriteVitalsValues(int currentHP, int maxHP, FixedString64Bytes displayName)
    {
        int safeMaxHP = Mathf.Max(1, maxHP);
        networkCurrentHP.Value = Mathf.Clamp(currentHP, 0, safeMaxHP);
        networkMaxHP.Value = safeMaxHP;
        networkDisplayName.Value = displayName;
    }

    private void WriteOwnerWeaponVisual()
    {
        WriteWeaponVisualValues(
            ResolveOwnerWeaponModelName(),
            ResolveOwnerWeaponArmed(),
            ResolveOwnerWeaponIsArtifact(),
            ResolveOwnerWeaponIsTool());
    }

    private void WriteWeaponVisualValues(
        FixedString128Bytes modelName,
        bool armed,
        bool isArtifact,
        bool isTool)
    {
        networkWeaponModelName.Value = modelName;
        networkWeaponArmed.Value = armed;
        networkWeaponIsArtifact.Value = isArtifact;
        networkWeaponIsTool.Value = isTool;
    }

    private Transform ResolveControlledTransform()
    {
        return playerDriver != null ? playerDriver.transform : transform;
    }

    private void ApplyCameraOwnershipState(bool isLocalOwner)
    {
        CinemachineVirtualCameraBase[] virtualCameras = GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
        for (int i = 0; i < virtualCameras.Length; i++)
        {
            CinemachineVirtualCameraBase virtualCamera = virtualCameras[i];
            if (virtualCamera == null)
            {
                continue;
            }

            virtualCamera.Priority = isLocalOwner ? Mathf.Max(virtualCamera.Priority, 10) : -100;
            virtualCamera.gameObject.SetActive(isLocalOwner);
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].enabled = isLocalOwner;
            }
        }

        AudioListener[] audioListeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < audioListeners.Length; i++)
        {
            if (audioListeners[i] != null)
            {
                audioListeners[i].enabled = isLocalOwner;
            }
        }
    }

    private void ApplyGameplayOwnershipState(bool isLocalOwner)
    {
        SetPlayerInputsEnabled(false);

        BuffComponent[] buffComponents = GetComponentsInChildren<BuffComponent>(true);
        for (int i = 0; i < buffComponents.Length; i++)
        {
            BuffComponent buffComponent = buffComponents[i];
            if (buffComponent != null)
            {
                buffComponent.enabled = isLocalOwner;
            }
        }

        PlayerWeaponModeController[] weaponModeControllers = GetComponentsInChildren<PlayerWeaponModeController>(true);
        for (int i = 0; i < weaponModeControllers.Length; i++)
        {
            PlayerWeaponModeController controller = weaponModeControllers[i];
            if (controller != null)
            {
                controller.enabled = isLocalOwner;
            }
        }

        PlayerWeaponAttackEffectController[] attackEffectControllers = GetComponentsInChildren<PlayerWeaponAttackEffectController>(true);
        for (int i = 0; i < attackEffectControllers.Length; i++)
        {
            PlayerWeaponAttackEffectController controller = attackEffectControllers[i];
            if (controller != null)
            {
                controller.SetRemoteVisualOnly(!isLocalOwner);
                controller.enabled = true;
                if (!isLocalOwner)
                {
                    weaponAttackEffectController = controller;
                }
            }
        }

        PlayerAttackController[] attackControllers = GetComponentsInChildren<PlayerAttackController>(true);
        for (int i = 0; i < attackControllers.Length; i++)
        {
            PlayerAttackController controller = attackControllers[i];
            if (controller != null)
            {
                controller.enabled = isLocalOwner;
            }
        }

        PlayerWeaponTrailController[] trailControllers = GetComponentsInChildren<PlayerWeaponTrailController>(true);
        for (int i = 0; i < trailControllers.Length; i++)
        {
            PlayerWeaponTrailController controller = trailControllers[i];
            if (controller != null)
            {
                controller.enabled = isLocalOwner;
            }
        }

        PlayerWeaponModelController[] modelControllers = GetComponentsInChildren<PlayerWeaponModelController>(true);
        for (int i = 0; i < modelControllers.Length; i++)
        {
            PlayerWeaponModelController controller = modelControllers[i];
            if (controller != null)
            {
                controller.SetNetworkVisualMode(!isLocalOwner);
                if (!isLocalOwner)
                {
                    weaponModelController = controller;
                }
            }
        }
    }

    private void DisablePlayerInputsUntilOwnershipKnown()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        SetPlayerInputsEnabled(false);
    }

    private void SetPlayerInputsEnabled(bool enabled)
    {
        PlayerInput[] playerInputs = GetComponentsInChildren<PlayerInput>(true);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            PlayerInput playerInput = playerInputs[i];
            if (playerInput != null)
            {
                playerInput.enabled = enabled;
            }
        }
    }

    private void InitializeLocalGameplayState()
    {
        if (playerDriver == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerDriver.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = playerDriver.gameObject.AddComponent<PlayerHealth>();
        }

        BindOwnerHealth(playerHealth);
        playerHealth.InitializeFromGameData();
        GameMgr.Equipment?.ApplyEquipmentStatsToPlayerData();
        SyncOwnerVitalsFromHealth(playerHealth);
    }

    private void BindOwnerHealth(PlayerHealth playerHealth)
    {
        if (boundOwnerHealth == playerHealth)
        {
            return;
        }

        UnbindOwnerHealth();
        boundOwnerHealth = playerHealth;
        if (boundOwnerHealth != null)
        {
            boundOwnerHealth.OnHealthChanged -= HandleOwnerHealthChanged;
            boundOwnerHealth.OnHealthChanged += HandleOwnerHealthChanged;
        }
    }

    private void UnbindOwnerHealth()
    {
        if (boundOwnerHealth == null)
        {
            return;
        }

        boundOwnerHealth.OnHealthChanged -= HandleOwnerHealthChanged;
        boundOwnerHealth = null;
    }

    private void HandleOwnerHealthChanged(PlayerHealth health)
    {
        SyncOwnerVitalsFromHealth(health);
    }

    private void SyncOwnerVitalsFromHealth(PlayerHealth health)
    {
        if (health == null || !IsSpawned || !IsOwner)
        {
            return;
        }

        int maxHP = health.IsInitialized && health.MaxHP > 0 ? health.MaxHP : ResolveOwnerMaxHP();
        int currentHP = health.IsInitialized ? health.CurrentHP : ResolveOwnerCurrentHP();
        FixedString64Bytes displayName = ResolveOwnerDisplayName();

        if (IsServer)
        {
            WriteVitalsValues(currentHP, maxHP, displayName);
            return;
        }

        SubmitOwnerVitalsServerRpc(currentHP, maxHP, displayName);
    }

    private static void RebindSceneCamerasToLocalPlayer()
    {
        ThirdPersonCameraComtrol[] thirdPersonCameras = Object.FindObjectsOfType<ThirdPersonCameraComtrol>(true);
        for (int i = 0; i < thirdPersonCameras.Length; i++)
        {
            thirdPersonCameras[i]?.RebindToCurrentPlayer();
        }

        CameraControl[] cameraControls = Object.FindObjectsOfType<CameraControl>(true);
        for (int i = 0; i < cameraControls.Length; i++)
        {
            cameraControls[i]?.RebindToCurrentPlayer();
        }
    }

    private Animator ResolveAnimator()
    {
        if (playerDriver != null &&
            playerDriver.ctx != null &&
            PlayerStateDriver.HasPlayableAnimator(playerDriver.ctx.anim))
        {
            return playerDriver.ctx.anim;
        }

        Animator[] candidates = playerDriver != null
            ? playerDriver.GetComponentsInChildren<Animator>(true)
            : GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            Animator candidate = candidates[i];
            if (candidate != null && candidate.runtimeAnimatorController != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private int ResolveOwnerAnimState()
    {
        if (playerDriver == null || playerDriver.ctx == null)
        {
            return (int)NetworkPlayerAnimState.Idle;
        }

        PlayerContext ctx = playerDriver.ctx;
        if (!ctx.grounded)
        {
            if (ctx.isFlying || ctx.isGliding)
            {
                return (int)NetworkPlayerAnimState.Fly;
            }

            return ctx.ySpeed > 0.05f
                ? (int)NetworkPlayerAnimState.Jump
                : (int)NetworkPlayerAnimState.Fall;
        }

        if (ctx.weaponAttackPlaying || ctx.isLeftPressed)
        {
            return (int)NetworkPlayerAnimState.Attack;
        }

        if (ctx.move.sqrMagnitude > 0.0001f)
        {
            return ctx.isWalkingState
                ? (int)NetworkPlayerAnimState.Walk
                : (int)NetworkPlayerAnimState.Run;
        }

        return (int)NetworkPlayerAnimState.Idle;
    }

    private int ResolveOwnerCurrentHP()
    {
        PlayerHealth health = playerDriver != null ? playerDriver.GetComponent<PlayerHealth>() : null;
        if (health != null && health.IsInitialized)
        {
            return health.CurrentHP;
        }

        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data != null)
        {
            return data.currentHP;
        }

        return ResolveOwnerMaxHP();
    }

    private int ResolveOwnerMaxHP()
    {
        PlayerHealth health = playerDriver != null ? playerDriver.GetComponent<PlayerHealth>() : null;
        if (health != null && health.IsInitialized && health.MaxHP > 0)
        {
            return health.MaxHP;
        }

        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data != null)
        {
            return Mathf.Max(1, data.GetMaxHP());
        }

        return 100;
    }

    private FixedString64Bytes ResolveOwnerDisplayName()
    {
        string displayName = string.Empty;
        if (GameMgr.Account != null && GameMgr.Account.CurrentProfile != null)
        {
            displayName = GameMgr.Account.CurrentProfile.displayName;
        }

        if (string.IsNullOrWhiteSpace(displayName) && GameMgr.File != null && GameMgr.File.CurrentGameFile != null)
        {
            displayName = GameMgr.File.CurrentGameFile.playerName;
        }

        if (string.IsNullOrWhiteSpace(displayName) && GameMgr.Account != null && GameMgr.Account.CurrentProfile != null)
        {
            displayName = GameMgr.Account.CurrentProfile.playerId;
        }

        return string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
    }

    private FixedString128Bytes ResolveOwnerWeaponModelName()
    {
        WeaponItem activeWeapon = ResolveOwnerActiveWeapon();
        string modelName = activeWeapon != null ? activeWeapon.modelName : string.Empty;
        return string.IsNullOrWhiteSpace(modelName) ? string.Empty : modelName.Trim();
    }

    private FixedString128Bytes ResolveOwnerWeaponAttackEffectAddressKey()
    {
        PlayerWeaponAttackEffectController controller = ResolveWeaponAttackEffectController();
        string key = controller != null ? controller.CurrentEffectAddressKey : string.Empty;
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private FixedString128Bytes ResolveOwnerWeaponAttackEffectWeaponName()
    {
        PlayerWeaponAttackEffectController controller = ResolveWeaponAttackEffectController();
        string weaponName = controller != null ? controller.CurrentEffectWeaponName : string.Empty;
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            WeaponItem activeWeapon = ResolveOwnerActiveWeapon();
            weaponName = activeWeapon != null ? activeWeapon.name : string.Empty;
        }

        return string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName.Trim();
    }

    private bool ResolveOwnerWeaponArmed()
    {
        PlayerWeaponModeController weaponMode = playerDriver != null
            ? playerDriver.GetComponent<PlayerWeaponModeController>()
            : GetComponentInChildren<PlayerWeaponModeController>(true);
        return weaponMode != null && weaponMode.IsArmed;
    }

    private bool ResolveOwnerWeaponIsArtifact()
    {
        WeaponItem activeWeapon = ResolveOwnerActiveWeapon();
        return activeWeapon != null && activeWeapon.IsArtifactWeapon;
    }

    private bool ResolveOwnerWeaponIsTool()
    {
        WeaponItem activeWeapon = ResolveOwnerActiveWeapon();
        return activeWeapon != null && activeWeapon.IsTool;
    }

    private static WeaponItem ResolveOwnerActiveWeapon()
    {
        return GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;
    }

    private void ApplyRemoteWeaponVisualIfChanged()
    {
        string modelName = networkWeaponModelName.Value.ToString();
        bool armed = networkWeaponArmed.Value;
        bool isArtifact = networkWeaponIsArtifact.Value;
        bool isTool = networkWeaponIsTool.Value;

        if (hasAppliedRemoteWeaponVisual &&
            string.Equals(lastAppliedRemoteWeaponModelName, modelName, System.StringComparison.Ordinal) &&
            lastAppliedRemoteWeaponArmed == armed &&
            lastAppliedRemoteWeaponIsArtifact == isArtifact &&
            lastAppliedRemoteWeaponIsTool == isTool)
        {
            return;
        }

        if (weaponModelController == null)
        {
            weaponModelController = GetComponent<PlayerWeaponModelController>() ??
                                    GetComponentInChildren<PlayerWeaponModelController>(true);
        }

        if (weaponModelController == null)
        {
            return;
        }

        ApplyRemoteWeaponMode(armed);
        weaponModelController.SetNetworkVisualMode(true);
        weaponModelController.ApplyNetworkWeaponVisual(modelName, armed, isArtifact, isTool).Forget();
        hasAppliedRemoteWeaponVisual = true;
        lastAppliedRemoteWeaponModelName = modelName;
        lastAppliedRemoteWeaponArmed = armed;
        lastAppliedRemoteWeaponIsArtifact = isArtifact;
        lastAppliedRemoteWeaponIsTool = isTool;
    }

    private PlayerWeaponAttackEffectController ResolveWeaponAttackEffectController()
    {
        if (weaponAttackEffectController == null)
        {
            weaponAttackEffectController = GetComponent<PlayerWeaponAttackEffectController>() ??
                                           GetComponentInChildren<PlayerWeaponAttackEffectController>(true);
        }

        return weaponAttackEffectController;
    }

    private void PlayRemoteWeaponAttackEffect(
        string effectAddressKey,
        string effectWeaponName,
        string weaponModelName,
        bool weaponArmed)
    {
        ResolveWeaponAttackEffectController();

        if (weaponAttackEffectController == null && playerDriver != null)
        {
            weaponAttackEffectController = playerDriver.gameObject.AddComponent<PlayerWeaponAttackEffectController>();
        }

        if (weaponAttackEffectController == null)
        {
            return;
        }

        ApplyRemoteWeaponMode(weaponArmed || !string.IsNullOrWhiteSpace(weaponModelName));
        weaponAttackEffectController.SetRemoteVisualOnly(true);
        weaponAttackEffectController.enabled = true;
        weaponAttackEffectController.PlayRemoteAttackEffect(effectAddressKey, effectWeaponName, weaponModelName);
    }

    private void ApplyRemoteWeaponMode(bool armed)
    {
        PlayerWeaponModeController weaponModeController = playerDriver != null
            ? playerDriver.GetComponent<PlayerWeaponModeController>()
            : GetComponentInChildren<PlayerWeaponModeController>(true);
        if (weaponModeController == null)
        {
            return;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            animator = ResolveAnimator();
        }

        weaponModeController.ApplyNetworkVisualMode(armed, animator).Forget();
        lastRemoteAnimatorController = null;
        lastRemoteAnimState = -1;
    }

    private void PlayRemoteAnimState()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            animator = ResolveAnimator();
        }

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (lastRemoteAnimator != animator || lastRemoteAnimatorController != animator.runtimeAnimatorController)
        {
            lastRemoteAnimator = animator;
            lastRemoteAnimatorController = animator.runtimeAnimatorController;
            lastRemoteAnimState = -1;
        }

        int animState = networkAnimState.Value;
        if (animState == lastRemoteAnimState)
        {
            return;
        }

        string stateName = ResolveAnimStateName((NetworkPlayerAnimState)animState);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        if (!animator.HasState(0, Animator.StringToHash(stateName)))
        {
            return;
        }

        lastRemoteAnimState = animState;
        float blendDuration = animState == (int)NetworkPlayerAnimState.Attack ? 0.08f : 0.12f;
        animator.CrossFade(stateName, blendDuration, 0);
    }

    private string ResolveAnimStateName(NetworkPlayerAnimState animState)
    {
        PlayerContext ctx = playerDriver != null ? playerDriver.ctx : null;
        switch (animState)
        {
            case NetworkPlayerAnimState.Walk:
                return ctx != null ? ctx.walkAnimStateName : "Walk";
            case NetworkPlayerAnimState.Run:
                return ctx != null ? ctx.runAnimStateName : "Run";
            case NetworkPlayerAnimState.Jump:
                return ctx != null ? ctx.jumpAnimStateName : "Jump";
            case NetworkPlayerAnimState.Fall:
                return ctx != null ? ctx.fallAnimStateName : "Fall";
            case NetworkPlayerAnimState.Fly:
                return ctx != null ? ctx.flyAnimStateName : "Fly";
            case NetworkPlayerAnimState.Attack:
                return ctx != null ? ctx.attackAnimStateName : "ATKTree";
            default:
                return ctx != null ? ctx.idleAnimStateName : "Idle";
        }
    }

    private enum NetworkPlayerAnimState
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Jump = 3,
        Fall = 4,
        Fly = 5,
        Attack = 6
    }
}
