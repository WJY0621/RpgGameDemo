using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class RoleModelController : MonoBehaviour
{
    [Header("模型容器")]
    [SerializeField] private Transform modelContainer;

    [Header("渲染设置")]
    [SerializeField] private Camera modelRenderCamera;

    // 单例模式
    public static RoleModelController Instance { get; private set; }

    // 模型缓存
    private Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();

    // 当前加载的模型
    private GameObject currentModel;
    private string currentModelName;

    // 当前选中的角色数据
    private RoleData selectedRoleData;

    // 动画控制
    private Animator currentAnimator;
    private bool isRandomIdleRunning;
    private UniTask randomIdleTask;
    private int switchModelVersion;

    // 事件：模型切换完成
    public System.Action<RoleData> onModelChanged;

    // 预加载进度事件
    public System.Action<int, int> onPreloadProgress;

    public RoleData SelectedRoleData => selectedRoleData;
    public GameObject CurrentModel => currentModel;
    public Transform ModelContainer => modelContainer;
    public Animator CurrentAnimator => currentAnimator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (modelContainer == null)
        {
            modelContainer = transform;
        }
    }

    /// <summary>
    /// 获取或创建实例（懒加载）
    /// </summary>
    public static RoleModelController GetOrCreate()
    {
        if (Instance == null)
        {
            // 创建 GameObject 并添加组件
            GameObject go = new GameObject("RoleModelController");
            var controller = go.AddComponent<RoleModelController>();
            return controller;
        }
        return Instance;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 预加载模型列表
    /// </summary>
    /// <param name="modelNames">模型名称列表</param>
    public async Task PreloadModels(List<string> modelNames)
    {
        if (modelNames == null || modelNames.Count == 0) return;

        int total = modelNames.Count;
        int current = 0;

        foreach (string modelName in modelNames)
        {
            if (string.IsNullOrEmpty(modelName)) continue;

            // 如果已经缓存，跳过
            if (modelCache.ContainsKey(modelName))
            {
                current++;
                onPreloadProgress?.Invoke(current, total);
                continue;
            }

            // 异步加载模型
            GameObject modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(modelName);
            if (modelPrefab != null)
            {
                // 缓存预制体（不实例化）
                modelCache[modelName] = modelPrefab;
            }

            current++;
            onPreloadProgress?.Invoke(current, total);
        }
    }

    /// <summary>
    /// 预加载所有角色模型
    /// </summary>
    public async Task PreloadAllRoleModels(RoleListData roleListData)
    {
        if (roleListData == null || roleListData.roleList == null) return;

        List<string> modelNames = new List<string>();
        foreach (var roleData in roleListData.roleList)
        {
            string roleModelName;
            roleListData.GetRoleResourceNames(roleData.roleID, roleData.roleSex,
                out _, out _, out _, out roleModelName);
            modelNames.Add(roleModelName);
        }

        await PreloadModels(modelNames);
    }

    /// <summary>
    /// 切换角色模型
    /// </summary>
    /// <param name="roleData">角色数据（可选）</param>
    /// <param name="roleModelName">模型资源名称</param>
    public async Task SwitchModel(RoleData roleData, string roleModelName)
    {
        if (string.IsNullOrEmpty(roleModelName))
        {
            Debug.LogWarning("[RoleModelController] SwitchModel 收到空的 roleModelName，已跳过。");
            return;
        }

        int requestVersion = ++switchModelVersion;

        // 如果是同一个模型，不切换
        if (currentModelName == roleModelName && currentModel != null)
        {
            return;
        }

        // 停止之前的随机待机
        StopRandomIdle();

        // 清除容器中的所有子物体（包括默认模型）
        ClearModelContainer();

        currentModelName = roleModelName;
        selectedRoleData = roleData;

        if (modelContainer == null)
        {
            Debug.LogError("[RoleModelController] modelContainer 为空，无法实例化模型。请在 Inspector 中绑定模型容器。");
            return;
        }

        // 从缓存或异步加载模型
        if (modelCache.TryGetValue(roleModelName, out var modelPrefab))
        {
            // 从缓存实例化（瞬间完成）
            currentModel = Instantiate(modelPrefab, modelContainer);
        }
        else
        {
            // 缓存中没有，异步加载
            modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(roleModelName);
            if (requestVersion != switchModelVersion)
            {
                if (modelPrefab != null)
                {
                    modelCache[roleModelName] = modelPrefab;
                }

                return;
            }

            if (modelPrefab != null)
            {
                currentModel = Instantiate(modelPrefab, modelContainer);
                // 缓存起来
                modelCache[roleModelName] = modelPrefab;
            }
            else
            {
                Debug.LogError($"[RoleModelController] 加载模型资源失败: {roleModelName}（Addressables 中是否存在该资源？）");
            }
        }

        if (currentModel == null)
        {
            Debug.LogWarning($"[RoleModelController] 模型实例化失败: {roleModelName}，预览将为空。");
            return;
        }

        // 防御：确保模型能被渲染相机看到（相机开启 + Layer 不被 CullingMask 剔除）
        EnsureModelVisibleToCamera(currentModel);

        // 设置 Animator Controller
        await SetupAnimatorController(roleModelName, requestVersion);
        if (requestVersion != switchModelVersion)
        {
            return;
        }

        // 触发模型切换事件
        onModelChanged?.Invoke(selectedRoleData);
    }

    /// <summary>
    /// 根据模型名称设置对应的 Animator Controller
    /// </summary>
    private async Task SetupAnimatorController(string modelName, int requestVersion)
    {
        if (currentModel == null) return;

        currentAnimator = currentModel.GetComponent<Animator>();
        if (currentAnimator == null)
        {
            currentAnimator = currentModel.AddComponent<Animator>();
        }

        // 启用 Root Motion，使用动画自带的位置和旋转信息
        currentAnimator.applyRootMotion = true;

        // 从模型名称解析Animator Controller名称
        // 例如: RoleModel_Women_01 -> RoleAnimator_Women_01
        string animatorControllerName = GetAnimatorControllerName(modelName);

        if (!string.IsNullOrEmpty(animatorControllerName))
        {
            var controller = await GameMgr.AssetLoader.LoadAsset<UnityEngine.RuntimeAnimatorController>(animatorControllerName);
            if (requestVersion != switchModelVersion || currentModelName != modelName || currentAnimator == null)
            {
                return;
            }

            if (controller != null)
            {
                currentAnimator.runtimeAnimatorController = controller;

                // 启动随机待机
                StartRandomIdle();
            }
            else
            {
                Debug.LogWarning($"[RoleModelController] Failed to load Animator Controller: {animatorControllerName}");
            }
        }
    }

    /// <summary>
    /// 从模型名称获取 Animator Controller 名称
    /// </summary>
    private string GetAnimatorControllerName(string modelName)
    {
        // 例如: RoleModel_Women_01 -> RoleAnimator_Women_01
        if (modelName.StartsWith("RoleModel_"))
        {
            return "RoleAnimator" + modelName.Substring("RoleModel".Length);
        }
        return null;
    }

    /// <summary>
    /// 启动随机待机
    /// </summary>
    private void StartRandomIdle()
    {
        if (isRandomIdleRunning) return;
        isRandomIdleRunning = true;
        randomIdleTask = PlayRandomIdleLoop();
    }

    /// <summary>
    /// 停止随机待机
    /// </summary>
    private void StopRandomIdle()
    {
        isRandomIdleRunning = false;
    }

    /// <summary>
    /// 随机待机循环
    /// </summary>
    private async UniTask PlayRandomIdleLoop()
    {
        while (isRandomIdleRunning && currentAnimator != null)
        {
            // 等待待机动画播放完成
            await WaitForAnimationToEnd("Idle");

            if (!isRandomIdleRunning || !PlayerStateDriver.HasPlayableAnimator(currentAnimator)) break;

            // 直接切换到随机待机
            int randomIdle = Random.Range(1, 5);
            currentAnimator.SetInteger("RandomIdle", randomIdle);

            // 等待随机待机动画播放完成
            await WaitForAnimationToEnd("Idle");

            if (!isRandomIdleRunning || !PlayerStateDriver.HasPlayableAnimator(currentAnimator)) break;

            // 回到待机
            currentAnimator.SetInteger("RandomIdle", 0);
        }
    }

    /// <summary>
    /// 等待指定动画播放完成
    /// </summary>
    private async UniTask WaitForAnimationToEnd(string stateName)
    {
        while (isRandomIdleRunning && PlayerStateDriver.HasPlayableAnimator(currentAnimator))
        {
            AnimatorStateInfo stateInfo = currentAnimator.GetCurrentAnimatorStateInfo(0);

            // 检查当前状态是否为指定状态且播放进度接近结束
            if (stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 0.9f)
            {
                break;
            }
            await UniTask.Yield();
        }
    }

    /// <summary>
    /// 清除模型容器中的所有子物体
    /// </summary>
    private void ClearModelContainer()
    {
        if (modelContainer == null) return;

        // 销毁所有子物体
        foreach (Transform child in modelContainer)
        {
            if (child != null && child.gameObject != null)
            {
                Destroy(child.gameObject);
            }
        }

        currentModel = null;
        currentModelName = null;
    }

    /// <summary>
    /// 调整模型位置，使其居中显示
    /// </summary>
    private void AdjustModelPosition(GameObject model)
    {
        if (model == null) return;

        // 获取模型的包围盒
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // 计算中心偏移
        Vector3 centerOffset = bounds.center - model.transform.position;
        model.transform.localPosition = -centerOffset;

        // 根据包围盒大小调整缩放（可选）
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxSize > 2f)
        {
            float scale = 2f / maxSize;
            model.transform.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// 清除当前模型
    /// </summary>
    public void ClearModel()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
        currentModelName = null;
        selectedRoleData = null;
    }

    /// <summary>
    /// 设置相机目标纹理
    /// </summary>
    public void SetRenderTexture(RenderTexture renderTexture)
    {
        if (modelRenderCamera != null)
        {
            modelRenderCamera.targetTexture = renderTexture;
        }
    }

    /// <summary>
    /// 确保模型能被渲染相机看到：相机处于开启状态，且模型 Layer 不会被 CullingMask 剔除。
    /// 仅在确实会被剔除时才修改 Layer，正常情况下不改动任何东西。
    /// </summary>
    private void EnsureModelVisibleToCamera(GameObject model)
    {
        if (model == null) return;

        if (modelRenderCamera == null)
        {
            Debug.LogWarning("[RoleModelController] modelRenderCamera 未绑定，模型可能无法渲染到 RenderTexture。");
            return;
        }

        // 渲染相机必须开启才会持续渲染到 targetTexture
        if (!modelRenderCamera.enabled)
        {
            Debug.LogWarning("[RoleModelController] modelRenderCamera 处于禁用状态，已自动开启。");
            modelRenderCamera.enabled = true;
        }

        // 若 targetTexture 丢失则给出提示（不强行赋值，避免覆盖 Inspector 配置）
        if (modelRenderCamera.targetTexture == null)
        {
            Debug.LogWarning("[RoleModelController] modelRenderCamera.targetTexture 为空，RawImage 将无法显示内容。");
        }

        int mask = modelRenderCamera.cullingMask;
        // 模型当前 Layer 已在 CullingMask 内 —— 正常情况，直接返回
        if ((mask & (1 << model.layer)) != 0)
        {
            return;
        }

        // 否则切到 CullingMask 中第一个可见 Layer，避免模型被静默剔除
        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask & (1 << layer)) != 0)
            {
                Debug.LogWarning($"[RoleModelController] 模型 Layer({model.layer}) 不在渲染相机 CullingMask 内，已切换到 Layer {layer}。");
                SetLayerRecursively(model, layer);
                return;
            }
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
