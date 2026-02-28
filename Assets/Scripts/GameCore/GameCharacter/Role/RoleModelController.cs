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

    // 事件：模型切换完成
    public System.Action<RoleData> onModelChanged;

    // 预加载进度事件
    public System.Action<int, int> onPreloadProgress;

    public RoleData SelectedRoleData => selectedRoleData;
    public GameObject CurrentModel => currentModel;
    public Transform ModelContainer => modelContainer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
                Debug.Log($"[RoleModelController] Preloaded model: {modelName}");
            }
            else
            {
                Debug.LogWarning($"[RoleModelController] Failed to preload model: {modelName}");
            }

            current++;
            onPreloadProgress?.Invoke(current, total);
        }

        Debug.Log($"[RoleModelController] Preload complete! Cached {modelCache.Count} models.");
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
    /// <param name="roleData">角色数据</param>
    /// <param name="roleModelName">模型资源名称</param>
    public async Task SwitchModel(RoleData roleData, string roleModelName)
    {
        if (roleData == null || string.IsNullOrEmpty(roleModelName))
        {
            Debug.LogWarning("[RoleModelController] Invalid role data or model name!");
            return;
        }

        // 如果是同一个模型，不切换
        if (currentModelName == roleModelName && currentModel != null)
        {
            Debug.Log($"[RoleModelController] Model {roleModelName} is already loaded.");
            return;
        }

        Debug.Log($"[RoleModelController] Switching to model: {roleModelName}");

        // 清除容器中的所有子物体（包括默认模型）
        ClearModelContainer();

        currentModelName = roleModelName;
        selectedRoleData = roleData;

        // 从缓存或异步加载模型
        GameObject modelPrefab = null;
        if (modelCache.TryGetValue(roleModelName, out modelPrefab))
        {
            // 从缓存实例化（瞬间完成）
            if (modelContainer != null)
            {
                currentModel = Instantiate(modelPrefab, modelContainer);
                Debug.Log($"[RoleModelController] Model {roleModelName} loaded from cache!");
            }
        }
        else
        {
            // 缓存中没有，异步加载
            modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(roleModelName);
            if (modelPrefab != null)
            {
                if (modelContainer != null)
                {
                    currentModel = Instantiate(modelPrefab, modelContainer);
                    // 缓存起来
                    modelCache[roleModelName] = modelPrefab;
                    Debug.Log($"[RoleModelController] Model {roleModelName} loaded and cached!");
                }
            }
            else
            {
                Debug.LogError($"[RoleModelController] Failed to load model: {roleModelName}");
            }
        }

        // 触发模型切换事件
        onModelChanged?.Invoke(selectedRoleData);
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
}
