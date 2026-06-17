using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 特效管理器，通过 GameMgr.VFX 全局访问。
/// 对外提供三种播放方式：
///   Play()        — 世界坐标一次性特效
///   PlayFollow()  — 跟随目标一次性特效
///   PlayLooping() — 循环特效，返回 VFXHandle 供手动停止
/// 内部使用对象池避免频繁创建销毁，资源通过 Addressables 按需加载。
/// </summary>
public class VFXMgr
{
    private VFXConfigSO config;
    private Transform poolRoot;

    private readonly Dictionary<string, Queue<PooledVFXController>> pool
        = new Dictionary<string, Queue<PooledVFXController>>();

    private readonly Dictionary<string, GameObject> loadedPrefabs
        = new Dictionary<string, GameObject>();

    private readonly HashSet<PooledVFXController> activeInstances
        = new HashSet<PooledVFXController>();

    // ── 初始化 ────────────────────────────────────────────────────────

    public async UniTask Init()
    {
        CreatePoolRoot();

        config = await GameMgr.AssetLoader.LoadAsset<VFXConfigSO>("VFXConfig");
        if (config == null)
        {
            Debug.LogError("[VFXMgr] VFXConfig not found! Please create it and register in Addressables.");
            return;
        }

        for (int i = 0; i < config.entries.Count; i++)
        {
            VFXEntry entry = config.entries[i];
            if (entry.preloadCount > 0)
            {
                await PreloadAsync(entry);
            }
        }

        Debug.Log($"[VFXMgr] Ready. {config.entries.Count} effects registered.");
    }

    // ── 公开 API ──────────────────────────────────────────────────────

    /// <summary>
    /// 在世界坐标播放一次性特效。
    /// 示例：GameMgr.VFX.Play(VFXKeys.MonsterHit, hitPosition);
    /// </summary>
    public void Play(string key, Vector3 position, Quaternion rotation = default)
    {
        if (rotation == default)
        {
            rotation = Quaternion.identity;
        }

        SpawnAsync(key, position, rotation, null, false, null).Forget();
    }

    /// <summary>
    /// 跟随目标播放一次性特效（特效位置随目标移动）。
    /// 示例：GameMgr.VFX.PlayFollow(VFXKeys.ItemPickup, itemTransform);
    /// </summary>
    public void PlayFollow(string key, Transform target, Vector3 offset = default)
    {
        Vector3 pos = target != null ? target.position + offset : Vector3.zero;
        SpawnAsync(key, pos, Quaternion.identity, target, false, null).Forget();
    }

    public void PlayFollow(string key, Transform target, Vector3 offset, Quaternion rotation)
    {
        Vector3 pos = target != null ? target.position + offset : Vector3.zero;
        if (rotation == default)
        {
            rotation = Quaternion.identity;
        }

        SpawnAsync(key, pos, rotation, target, false, null).Forget();
    }

    /// <summary>
    /// 播放跟随目标的循环特效，返回 VFXHandle。
    /// 调用方持有 handle，在合适时机调用 handle.Stop() 停止。
    /// 示例：
    ///   VFXHandle handle = GameMgr.VFX.PlayLooping(VFXKeys.HealAura, playerTransform);
    ///   handle.Stop(); // 停止
    /// </summary>
    public VFXHandle PlayLooping(string key, Transform target)
    {
        VFXHandle handle = new VFXHandle();
        Vector3 pos = target != null ? target.position : Vector3.zero;
        SpawnAsync(key, pos, Quaternion.identity, target, Vector3.zero, Quaternion.identity, true, handle).Forget();
        return handle;
    }

    public VFXHandle PlayLooping(string key, Vector3 position, Quaternion rotation = default)
    {
        VFXHandle handle = new VFXHandle();
        if (rotation == default)
        {
            rotation = Quaternion.identity;
        }

        SpawnAsync(key, position, rotation, null, Vector3.zero, Quaternion.identity, true, handle).Forget();
        return handle;
    }

    public VFXHandle PlayLooping(string key, Transform target, Vector3 offset, Quaternion rotation)
    {
        VFXHandle handle = new VFXHandle();
        Quaternion localRotation = rotation == default ? Quaternion.identity : rotation;
        Vector3 pos = target != null ? target.TransformPoint(offset) : offset;
        Quaternion worldRotation = target != null ? target.rotation * localRotation : localRotation;
        SpawnAsync(key, pos, worldRotation, target, offset, localRotation, true, handle).Forget();
        return handle;
    }

    /// <summary>停止并回收所有当前激活的特效。</summary>
    public void StopAll()
    {
        foreach (PooledVFXController ctrl in activeInstances)
        {
            if (ctrl != null)
            {
                ctrl.Stop();
            }
        }

        activeInstances.Clear();
    }

    // ── 内部实现 ──────────────────────────────────────────────────────

    private UniTaskVoid SpawnAsync(string key, Vector3 pos, Quaternion rot,
        Transform follow, bool loop, VFXHandle handle)
    {
        return SpawnAsync(key, pos, rot, follow, Vector3.zero, Quaternion.identity, loop, handle);
    }

    private async UniTaskVoid SpawnAsync(string key, Vector3 pos, Quaternion rot,
        Transform follow, Vector3 followOffset, Quaternion followRotation, bool loop, VFXHandle handle)
    {
        PooledVFXController ctrl = await GetOrCreateAsync(key);
        if (ctrl == null)
        {
            return;
        }

        activeInstances.Add(ctrl);
        handle?.Bind(ctrl);
        ctrl.Play(pos, rot, follow, followOffset, followRotation, loop);
    }

    private async UniTask<PooledVFXController> GetOrCreateAsync(string key)
    {
        // 先从池中取
        if (pool.TryGetValue(key, out Queue<PooledVFXController> queue) && queue.Count > 0)
        {
            return queue.Dequeue();
        }

        // 池空，按需加载预制体并创建新实例
        GameObject prefab = await GetPrefabAsync(key);
        if (prefab == null)
        {
            return null;
        }

        return CreateInstance(key, prefab);
    }

    private async UniTask<GameObject> GetPrefabAsync(string key)
    {
        if (loadedPrefabs.TryGetValue(key, out GameObject cached))
        {
            return cached;
        }

        VFXEntry entry = config?.GetEntry(key);
        if (entry == null)
        {
            Debug.LogWarning($"[VFXMgr] Key '{key}' not found in VFXConfig. Add it to the config first.");
            return null;
        }

        GameObject prefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(entry.address);
        if (prefab != null)
        {
            loadedPrefabs[key] = prefab;
        }

        return prefab;
    }

    private PooledVFXController CreateInstance(string key, GameObject prefab)
    {
        GameObject go = Object.Instantiate(prefab, poolRoot);
        go.name = $"VFX_{key}";
        go.SetActive(false);

        PooledVFXController ctrl = go.GetComponent<PooledVFXController>();
        if (ctrl == null)
        {
            ctrl = go.AddComponent<PooledVFXController>();
        }

        ctrl.Setup(key, OnEffectReturned);
        return ctrl;
    }

    private void OnEffectReturned(PooledVFXController ctrl)
    {
        activeInstances.Remove(ctrl);

        if (!pool.TryGetValue(ctrl.PoolKey, out Queue<PooledVFXController> queue))
        {
            queue = new Queue<PooledVFXController>();
            pool[ctrl.PoolKey] = queue;
        }

        queue.Enqueue(ctrl);
    }

    private async UniTask PreloadAsync(VFXEntry entry)
    {
        GameObject prefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(entry.address);
        if (prefab == null)
        {
            Debug.LogWarning($"[VFXMgr] Preload failed — key: '{entry.key}', address: '{entry.address}'.");
            return;
        }

        loadedPrefabs[entry.key] = prefab;

        if (!pool.TryGetValue(entry.key, out Queue<PooledVFXController> queue))
        {
            queue = new Queue<PooledVFXController>();
            pool[entry.key] = queue;
        }

        for (int i = 0; i < entry.preloadCount; i++)
        {
            queue.Enqueue(CreateInstance(entry.key, prefab));
        }
    }

    private void CreatePoolRoot()
    {
        GameObject go = new GameObject("[VFXPool]");
        Object.DontDestroyOnLoad(go);
        poolRoot = go.transform;
    }
}
