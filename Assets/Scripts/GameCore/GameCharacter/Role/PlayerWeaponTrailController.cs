using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 根据当前手持武器模型上的 D1/D2/D3 点位，驱动场景中的刀光拖尾渲染器。
/// 拖尾材质、渐变和启用方式由 WeaponTrailConfigSO 配置。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponTrailController : MonoBehaviour
{
    private const string TrailConfigAddress = "WeaponTrailConfig";
    private const string PointD1 = "D1";
    private const string PointD2 = "D2";
    private const string PointD3 = "D3";

    [SerializeField] private MonoBehaviour trailRenderer;

    private WeaponTrailConfigSO config;
    private WeaponTrailConfigEntry currentConfig;
    private WeaponTrailRendererAdapter trailAdapter;
    private PlayerAttackController attackController;
    private bool attackWindowOpen;
    private bool isBoundToWeapon;
    private int bindVersion;

    private void Awake()
    {
        attackController = GetComponent<PlayerAttackController>();
    }

    private void OnEnable()
    {
        if (IsRemoteNetworkReplica())
        {
            enabled = false;
            return;
        }

        if (GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            GameMgr.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
            GameMgr.Equipment.OnCurrentWeaponModelChanged -= HandleCurrentWeaponModelChanged;
            GameMgr.Equipment.OnCurrentWeaponModelChanged += HandleCurrentWeaponModelChanged;
        }

        if (attackController != null)
        {
            attackController.AttackWindowBegan -= HandleAttackWindowBegan;
            attackController.AttackWindowBegan += HandleAttackWindowBegan;
            attackController.AttackWindowEnded -= HandleAttackWindowEnded;
            attackController.AttackWindowEnded += HandleAttackWindowEnded;
        }

        LoadConfigAndBindAsync().Forget();
    }

    private void OnDisable()
    {
        if (GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            GameMgr.Equipment.OnCurrentWeaponModelChanged -= HandleCurrentWeaponModelChanged;
        }

        if (attackController != null)
        {
            attackController.AttackWindowBegan -= HandleAttackWindowBegan;
            attackController.AttackWindowEnded -= HandleAttackWindowEnded;
        }

        attackWindowOpen = false;
        SetTrailVisible(false);
    }

    private void Update()
    {
        if (!attackWindowOpen ||
            currentConfig == null ||
            currentConfig.enableMode != WeaponTrailEnableMode.AttackOnly)
        {
            return;
        }

        if (attackController == null || !attackController.IsAttackWindowOpen)
        {
            ForceStopAttackTrail();
        }
    }

    private void HandleEquipmentChanged()
    {
        ForceStopAttackTrail();
        BindCurrentWeaponAsync().Forget();
    }

    private void HandleCurrentWeaponModelChanged()
    {
        ForceStopAttackTrail();
        BindCurrentWeaponAsync().Forget();
    }

    private void HandleAttackWindowBegan()
    {
        attackWindowOpen = true;
        if (currentConfig != null && currentConfig.enableMode == WeaponTrailEnableMode.AttackOnly)
        {
            SetTrailVisible(true);
        }
    }

    private void HandleAttackWindowEnded()
    {
        attackWindowOpen = false;
        if (currentConfig != null && currentConfig.enableMode == WeaponTrailEnableMode.AttackOnly)
        {
            SetTrailVisible(false);
        }
    }

    private async UniTaskVoid LoadConfigAndBindAsync()
    {
        if (config == null)
        {
            config = await GameMgr.AssetLoader.LoadAsset<WeaponTrailConfigSO>(TrailConfigAddress);
            if (config == null)
            {
                Debug.LogWarning($"[PlayerWeaponTrailController] WeaponTrailConfig not found. Addressables key: {TrailConfigAddress}");
            }
        }

        BindCurrentWeaponAsync().Forget();
    }

    private async UniTaskVoid BindCurrentWeaponAsync()
    {
        int version = ++bindVersion;
        await UniTask.Yield();

        if (this == null || !isActiveAndEnabled || version != bindVersion)
        {
            return;
        }

        GameObject weaponObject = GameMgr.Equipment != null ? GameMgr.Equipment.CurrentWeaponObject : null;
        string weaponModelName = GameMgr.Equipment != null ? GameMgr.Equipment.CurrentWeaponModelName : string.Empty;
        WeaponItem activeWeapon = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;

        if (weaponObject == null || activeWeapon == null || activeWeapon.IsArtifactWeapon || activeWeapon.IsTool)
        {
            ClearBinding();
            return;
        }

        Transform d1 = FindChildRecursive(weaponObject.transform, PointD1);
        Transform d2 = FindChildRecursive(weaponObject.transform, PointD2);
        Transform d3 = FindChildRecursive(weaponObject.transform, PointD3);
        if (d1 == null || d2 == null || d3 == null)
        {
            Debug.LogWarning($"[PlayerWeaponTrailController] Missing D1/D2/D3 on weapon model: {weaponObject.name}");
            ClearBinding();
            return;
        }

        MonoBehaviour rendererComponent = trailRenderer != null ? trailRenderer : FindTrailRenderer();
        if (rendererComponent == null)
        {
            Debug.LogWarning("[PlayerWeaponTrailController] Trail renderer not found in scene.");
            ClearBinding();
            return;
        }

        currentConfig = config != null ? config.GetConfig(weaponModelName) : null;
        trailAdapter = new WeaponTrailRendererAdapter(rendererComponent);
        trailAdapter.ApplyPoints(new[] { d1, d2 }, d3);

        if (currentConfig != null)
        {
            trailAdapter.ApplyMaterial(currentConfig.trailMaterial);
            trailAdapter.ApplyGradient(currentConfig.trailGradient);
        }

        isBoundToWeapon = true;
        RefreshTrailVisibleState();
    }

    private void ClearBinding()
    {
        currentConfig = null;
        isBoundToWeapon = false;
        attackWindowOpen = false;
        SetTrailVisible(false);
    }

    private void ForceStopAttackTrail()
    {
        attackWindowOpen = false;
        if (currentConfig != null && currentConfig.enableMode == WeaponTrailEnableMode.AttackOnly)
        {
            SetTrailVisible(false);
        }
    }

    private void RefreshTrailVisibleState()
    {
        if (!isBoundToWeapon || currentConfig == null)
        {
            SetTrailVisible(false);
            return;
        }

        bool visible = currentConfig.enableMode == WeaponTrailEnableMode.AlwaysWhenEquipped ||
                       (currentConfig.enableMode == WeaponTrailEnableMode.AttackOnly && attackWindowOpen);
        SetTrailVisible(visible);
    }

    private void SetTrailVisible(bool visible)
    {
        if (trailAdapter == null || !trailAdapter.IsValid)
        {
            trailAdapter = null;
            return;
        }

        trailAdapter.SetVisible(visible);
    }

    private static MonoBehaviour FindTrailRenderer()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (WeaponTrailRendererAdapter.CanAdapt(behaviour))
            {
                return behaviour;
            }
        }

        return null;
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

    private sealed class WeaponTrailRendererAdapter
    {
        private readonly MonoBehaviour rendererComponent;
        private readonly MeshRenderer meshRenderer;
        private readonly MeshFilter meshFilter;
        private readonly FieldInfo bladePointsField;
        private readonly FieldInfo centerPointField;
        private readonly FieldInfo materialField;
        private readonly FieldInfo gradientField;

        public bool IsValid => rendererComponent != null;

        public WeaponTrailRendererAdapter(MonoBehaviour rendererComponent)
        {
            this.rendererComponent = rendererComponent;
            meshRenderer = rendererComponent.GetComponent<MeshRenderer>();
            meshFilter = rendererComponent.GetComponent<MeshFilter>();

            Type type = rendererComponent.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                Type fieldType = field.FieldType;

                if (bladePointsField == null && fieldType == typeof(Transform[]))
                {
                    bladePointsField = field;
                }
                else if (centerPointField == null && fieldType == typeof(Transform))
                {
                    centerPointField = field;
                }
                else if (materialField == null && fieldType == typeof(Material))
                {
                    materialField = field;
                }
                else if (gradientField == null && fieldType == typeof(Gradient))
                {
                    gradientField = field;
                }
            }
        }

        public static bool CanAdapt(MonoBehaviour component)
        {
            if (component == null)
            {
                return false;
            }

            Type type = component.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            bool hasBladePoints = false;
            bool hasCenterPoint = false;
            bool hasMaterial = false;
            bool hasGradient = false;

            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldType = fields[i].FieldType;
                hasBladePoints |= fieldType == typeof(Transform[]);
                hasCenterPoint |= fieldType == typeof(Transform);
                hasMaterial |= fieldType == typeof(Material);
                hasGradient |= fieldType == typeof(Gradient);
            }

            return hasBladePoints && hasCenterPoint && hasMaterial && hasGradient;
        }

        public void ApplyPoints(Transform[] bladePoints, Transform centerPoint)
        {
            if (!IsValid)
            {
                return;
            }

            bladePointsField?.SetValue(rendererComponent, bladePoints);
            centerPointField?.SetValue(rendererComponent, centerPoint);
            ClearMesh();
        }

        public void ApplyMaterial(Material material)
        {
            if (!IsValid || material == null)
            {
                return;
            }

            materialField?.SetValue(rendererComponent, material);
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = material;
            }
        }

        public void ApplyGradient(Gradient gradient)
        {
            if (!IsValid || gradient == null)
            {
                return;
            }

            gradientField?.SetValue(rendererComponent, gradient);
        }

        public void SetVisible(bool visible)
        {
            if (!IsValid)
            {
                return;
            }

            rendererComponent.enabled = visible;
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }

            if (!visible)
            {
                ClearMesh();
            }
        }

        private void ClearMesh()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            meshFilter.sharedMesh.Clear();
        }
    }
}
