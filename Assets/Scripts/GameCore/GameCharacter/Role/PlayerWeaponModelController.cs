using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 挂在 PlayerRole 根节点上，负责根据 EquipmentMgr 当前激活武器，
/// 将武器模型实例化到角色骨骼的 Weapon/Sword 挂点下。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponModelController : MonoBehaviour
{
    private const string WeaponRootName = "Weapon";
    private const string SwordMountName = "Sword";

    [Header("武器挂点偏移")]
    [SerializeField] private Vector3 weaponLocalPosition = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 weaponLocalEuler = new Vector3(0f, 180f, 180f);
    [SerializeField] private Vector3 artifactWeaponLocalEuler = new Vector3(-90f, 0f, 0f);

    private PlayerModelManager playerModelManager;
    private PlayerWeaponModeController weaponModeController;
    private Transform cachedWeaponMount;
    private GameObject currentWeaponObject;
    private string currentWeaponModelName;
    private string currentWeaponSlotKey;
    private int refreshVersion;
    private bool useNetworkVisuals;
    private string networkWeaponModelName = string.Empty;
    private bool networkWeaponArmed;
    private bool networkWeaponIsArtifact;
    private bool networkWeaponIsTool;

    public string CurrentWeaponModelName => currentWeaponModelName;
    public string CurrentWeaponSlotKey => currentWeaponSlotKey;
    public GameObject CurrentWeaponObject => currentWeaponObject;

    private void Awake()
    {
        playerModelManager = GetComponent<PlayerModelManager>();
        weaponModeController = GetComponentInParent<PlayerWeaponModeController>();
    }

    private void OnEnable()
    {
        useNetworkVisuals = IsRemoteNetworkReplica();

        if (weaponModeController == null)
        {
            weaponModeController = GetComponentInParent<PlayerWeaponModeController>();
        }

        if (!useNetworkVisuals && weaponModeController != null)
        {
            weaponModeController.OnArmedStateChanged -= HandleArmedStateChanged;
            weaponModeController.OnArmedStateChanged += HandleArmedStateChanged;
        }

        if (!useNetworkVisuals && GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            GameMgr.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }

        if (playerModelManager != null)
        {
            playerModelManager.onModelSwitched -= HandleModelSwitched;
            playerModelManager.onModelSwitched += HandleModelSwitched;
        }

        if (useNetworkVisuals)
        {
            ClearCurrentWeaponObject(false);
            ApplyNetworkWeaponVisual(
                networkWeaponModelName,
                networkWeaponArmed,
                networkWeaponIsArtifact,
                networkWeaponIsTool).Forget();
        }
        else
        {
            RefreshWeaponModel().Forget();
        }
    }

    private void OnDisable()
    {
        if (weaponModeController != null)
        {
            weaponModeController.OnArmedStateChanged -= HandleArmedStateChanged;
        }

        if (GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        if (playerModelManager != null)
        {
            playerModelManager.onModelSwitched -= HandleModelSwitched;
        }
    }

    private void OnDestroy()
    {
        ClearCurrentWeaponObject(!useNetworkVisuals);
    }

    private void HandleEquipmentChanged()
    {
        if (useNetworkVisuals)
        {
            return;
        }

        RefreshWeaponModel().Forget();
    }

    private void HandleModelSwitched(Animator _)
    {
        cachedWeaponMount = null;
        if (useNetworkVisuals)
        {
            ApplyNetworkWeaponVisual(
                networkWeaponModelName,
                networkWeaponArmed,
                networkWeaponIsArtifact,
                networkWeaponIsTool).Forget();
        }
        else
        {
            RefreshWeaponModel().Forget();
        }
    }

    private void HandleArmedStateChanged(bool _)
    {
        ApplyWeaponAttachmentForCurrentMode();
    }

    public async UniTaskVoid RefreshWeaponModel()
    {
        if (useNetworkVisuals)
        {
            return;
        }

        int version = ++refreshVersion;
        await UniTask.Yield();

        if (this == null || !isActiveAndEnabled || version != refreshVersion)
        {
            return;
        }

        WeaponItem activeWeapon = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;
        currentWeaponSlotKey = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveHandSlotKey() : string.Empty;

        if (activeWeapon == null || string.IsNullOrWhiteSpace(activeWeapon.modelName))
        {
            ClearCurrentWeaponObject();
            return;
        }

        Transform weaponMount = GetWeaponMount();
        if (weaponMount == null)
        {
            Debug.LogWarning("[PlayerWeaponModelController] Weapon mount not found.");
            return;
        }

        if (currentWeaponObject != null &&
            currentWeaponModelName == activeWeapon.modelName &&
            currentWeaponObject.transform.parent == weaponMount)
        {
            ApplyWeaponTransform(activeWeapon);
            ApplyWeaponAttachmentForCurrentMode();
            return;
        }

        GameObject modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(activeWeapon.modelName);
        if (this == null || !isActiveAndEnabled || version != refreshVersion)
        {
            return;
        }

        if (modelPrefab == null)
        {
            Debug.LogWarning($"[PlayerWeaponModelController] Weapon model not found: {activeWeapon.modelName}");
            ClearCurrentWeaponObject();
            return;
        }

        ClearCurrentWeaponObject();

        currentWeaponObject = Instantiate(modelPrefab, weaponMount, false);
        currentWeaponObject.name = modelPrefab.name;
        ApplyWeaponTransform(activeWeapon.IsArtifactWeapon, activeWeapon.IsTool);
        ApplyWeaponAttachmentForCurrentMode();

        currentWeaponModelName = activeWeapon.modelName;
        GameMgr.Equipment?.SetCurrentWeaponModelInfo(currentWeaponSlotKey, currentWeaponModelName, currentWeaponObject);
    }

    private void ApplyWeaponTransform(WeaponItem activeWeapon)
    {
        if (activeWeapon == null)
        {
            return;
        }

        ApplyWeaponTransform(activeWeapon.IsArtifactWeapon, activeWeapon.IsTool);
    }

    private void ApplyWeaponTransform(bool isArtifactWeapon, bool isTool)
    {
        if (currentWeaponObject == null || isTool)
        {
            return;
        }

        if (!isArtifactWeapon)
        {
            currentWeaponObject.transform.localPosition = weaponLocalPosition;
        }

        Vector3 localEuler = isArtifactWeapon ? artifactWeaponLocalEuler : weaponLocalEuler;
        currentWeaponObject.transform.localRotation = Quaternion.Euler(localEuler);
        currentWeaponObject.transform.localScale = Vector3.one;
    }

    public void SetNetworkVisualMode(bool enabled)
    {
        if (useNetworkVisuals == enabled)
        {
            return;
        }

        useNetworkVisuals = enabled;
        refreshVersion++;

        if (useNetworkVisuals)
        {
            if (weaponModeController != null)
            {
                weaponModeController.OnArmedStateChanged -= HandleArmedStateChanged;
            }

            if (GameMgr.Equipment != null)
            {
                GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            }

            ClearCurrentWeaponObject(false);
            ApplyNetworkWeaponVisual(
                networkWeaponModelName,
                networkWeaponArmed,
                networkWeaponIsArtifact,
                networkWeaponIsTool).Forget();
            return;
        }

        if (weaponModeController != null)
        {
            weaponModeController.OnArmedStateChanged -= HandleArmedStateChanged;
            weaponModeController.OnArmedStateChanged += HandleArmedStateChanged;
        }

        if (GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            GameMgr.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }

        RefreshWeaponModel().Forget();
    }

    public async UniTaskVoid ApplyNetworkWeaponVisual(
        string modelName,
        bool isArmed,
        bool isArtifactWeapon,
        bool isTool)
    {
        useNetworkVisuals = true;
        networkWeaponModelName = modelName ?? string.Empty;
        networkWeaponArmed = isArmed;
        networkWeaponIsArtifact = isArtifactWeapon;
        networkWeaponIsTool = isTool;

        int version = ++refreshVersion;
        await UniTask.Yield();

        if (this == null || !isActiveAndEnabled || version != refreshVersion)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(networkWeaponModelName))
        {
            ClearCurrentWeaponObject(false);
            return;
        }

        Transform weaponMount = GetWeaponMount();
        if (weaponMount == null)
        {
            return;
        }

        if (currentWeaponObject != null &&
            string.Equals(currentWeaponModelName, networkWeaponModelName, System.StringComparison.Ordinal) &&
            currentWeaponObject.transform.parent == weaponMount)
        {
            ApplyWeaponTransform(networkWeaponIsArtifact, networkWeaponIsTool);
            ApplyWeaponAttachmentForCurrentMode();
            return;
        }

        GameObject modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(networkWeaponModelName);
        if (this == null || !isActiveAndEnabled || version != refreshVersion)
        {
            return;
        }

        if (modelPrefab == null)
        {
            ClearCurrentWeaponObject(false);
            return;
        }

        ClearCurrentWeaponObject(false);
        currentWeaponObject = Instantiate(modelPrefab, weaponMount, false);
        currentWeaponObject.name = modelPrefab.name;
        currentWeaponModelName = networkWeaponModelName;
        currentWeaponSlotKey = string.Empty;
        ApplyWeaponTransform(networkWeaponIsArtifact, networkWeaponIsTool);
        ApplyWeaponAttachmentForCurrentMode();
    }

    private Transform GetWeaponMount()
    {
        if (cachedWeaponMount != null)
        {
            return cachedWeaponMount;
        }

        if (playerModelManager == null)
        {
            playerModelManager = GetComponent<PlayerModelManager>();
            if (playerModelManager == null)
            {
                return null;
            }
        }

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return null;
        }

        Transform weaponRoot = FindChildRecursive(animator.transform, WeaponRootName);
        if (weaponRoot == null)
        {
            return null;
        }

        Transform swordMount = FindChildRecursive(weaponRoot, SwordMountName);
        cachedWeaponMount = swordMount != null ? swordMount : weaponRoot;
        return cachedWeaponMount;
    }

    private void ApplyWeaponAttachmentForCurrentMode()
    {
        PlayerWeaponAttachmentController attachmentController = GetComponentInChildren<PlayerWeaponAttachmentController>(true);
        if (attachmentController == null)
        {
            return;
        }

        if (useNetworkVisuals)
        {
            if (networkWeaponArmed)
            {
                attachmentController.AttachWeaponToHand();
            }
            else
            {
                attachmentController.AttachWeaponToBack();
            }

            return;
        }

        if (weaponModeController == null)
        {
            weaponModeController = GetComponentInParent<PlayerWeaponModeController>();
        }

        if (weaponModeController != null && weaponModeController.IsArmed)
        {
            attachmentController.AttachWeaponToHand();
        }
        else
        {
            attachmentController.AttachWeaponToBack();
        }
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private bool IsRemoteNetworkReplica()
    {
        Unity.Netcode.NetworkObject networkObject = GetComponentInParent<Unity.Netcode.NetworkObject>();
        return networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner;
    }

    private void ClearCurrentWeaponObject(bool notifyEquipment = true)
    {
        if (currentWeaponObject != null)
        {
            Destroy(currentWeaponObject);
            currentWeaponObject = null;
        }

        currentWeaponModelName = string.Empty;
        currentWeaponSlotKey = string.Empty;
        if (notifyEquipment)
        {
            GameMgr.Equipment?.ClearCurrentWeaponModelInfo();
        }
    }
}
