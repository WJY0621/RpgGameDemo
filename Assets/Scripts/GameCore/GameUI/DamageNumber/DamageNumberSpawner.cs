using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 浮动伤害数字静态生成器。
/// 调用方式：DamageNumberSpawner.Show(damage, worldPos)
///
/// 加载方式：使用项目自带 GameMgr.AssetLoader（Addressables）按 PrefabAddress 异步加载，
/// prefab 不需要放在 Resources 目录下，只需在 Addressables 里挂上对应地址。
///
/// 池化策略：
///   - 内部维护 Queue&lt;DamageNumber&gt; 池，按需扩容
///   - 首次 Show 触发异步加载，期间的请求暂存到 pendingSpawns 队列，加载完成后批量执行
///   - 跨场景：场景切换会销毁池根节点，下一次访问时自动重建（旧的 dead refs 在 GetFromPool 中被跳过）
/// </summary>
public static class DamageNumberSpawner
{
    public const string PrefabAddress = "DamageNumber";

    private static GameObject prefab;
    private static bool isLoading;
    private static Transform poolRoot;
    private static Canvas poolCanvas;

    private static readonly Queue<DamageNumber> pool = new Queue<DamageNumber>();
    private static readonly Queue<PendingSpawn> pendingSpawns = new Queue<PendingSpawn>();

    private struct PendingSpawn
    {
        public int damage;
        public Vector3 worldPos;
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────

    public static void Show(int damage, Vector3 worldPos)
    {
        if (damage <= 0)
            return;

        if (prefab != null)
        {
            SpawnNow(damage, worldPos);
            return;
        }

        // Prefab 尚未加载（或被 AssetLoader.Clear 释放）：暂存请求
        pendingSpawns.Enqueue(new PendingSpawn { damage = damage, worldPos = worldPos });
        if (!isLoading)
        {
            isLoading = true;
            LoadAndFlushAsync().Forget();
        }
    }

    /// <summary>
    /// 可选：在场景加载时预热池子，避免战斗开局首次 Show 的异步加载延迟。
    /// </summary>
    public static async UniTask Preload(int count)
    {
        if (count <= 0)
            return;

        if (prefab == null)
        {
            await EnsurePrefabLoadedAsync();
            if (prefab == null)
                return;
        }

        Transform root = GetOrCreatePoolRoot();
        for (int i = 0; i < count; i++)
        {
            DamageNumber dn = CreateInstance(root);
            if (dn == null)
                break;
            dn.gameObject.SetActive(false);
            pool.Enqueue(dn);
        }
    }

    /// <summary>
    /// 由 DamageNumber 在动画结束后调用，归还自身到池中。
    /// </summary>
    public static void Return(DamageNumber dn)
    {
        if (dn == null)
            return;

        if (dn.gameObject.activeSelf)
            dn.gameObject.SetActive(false);

        Transform root = GetOrCreatePoolRoot();
        if (dn.transform.parent != root)
            dn.transform.SetParent(root, false);

        pool.Enqueue(dn);
    }

    /// <summary>
    /// 清空所有池实例。一般无需手动调用，场景切换时 GameObject 自动销毁，
    /// 池中的 dead refs 会在下次 Get 时被跳过。
    /// </summary>
    public static void Clear()
    {
        while (pool.Count > 0)
        {
            DamageNumber dn = pool.Dequeue();
            if (dn != null)
                Object.Destroy(dn.gameObject);
        }
        pendingSpawns.Clear();

        if (poolRoot != null)
        {
            Object.Destroy(poolRoot.gameObject);
            poolRoot = null;
            poolCanvas = null;
        }
    }

    // ──────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────

    private static async UniTaskVoid LoadAndFlushAsync()
    {
        await EnsurePrefabLoadedAsync();
        isLoading = false;

        if (prefab == null)
        {
            Debug.LogError($"[DamageNumberSpawner] Addressables 加载 prefab 失败: {PrefabAddress}");
            pendingSpawns.Clear();
            return;
        }

        while (pendingSpawns.Count > 0)
        {
            PendingSpawn p = pendingSpawns.Dequeue();
            SpawnNow(p.damage, p.worldPos);
        }
    }

    private static async UniTask EnsurePrefabLoadedAsync()
    {
        if (prefab != null)
            return;

        if (GameMgr.AssetLoader == null)
        {
            Debug.LogError("[DamageNumberSpawner] GameMgr.AssetLoader 不存在");
            return;
        }

        prefab = await GameMgr.AssetLoader.LoadPrefab(PrefabAddress);
    }

    private static void SpawnNow(int damage, Vector3 worldPos)
    {
        DamageNumber dn = GetOrCreate();
        if (dn == null)
            return;

        dn.PrepareForOverlay(poolCanvas);
        dn.gameObject.SetActive(true);
        dn.Play(damage, worldPos);
    }

    private static DamageNumber GetOrCreate()
    {
        // 优先复用池中的实例；跳过被场景切换销毁的 dead ref
        while (pool.Count > 0)
        {
            DamageNumber dn = pool.Dequeue();
            if (dn != null)
                return dn;
        }

        if (prefab == null)
            return null;

        Transform root = GetOrCreatePoolRoot();
        return CreateInstance(root);
    }

    private static DamageNumber CreateInstance(Transform parent)
    {
        GameObject obj = Object.Instantiate(prefab, parent);
        DamageNumber dn = obj.GetComponent<DamageNumber>();
        if (dn == null)
        {
            Debug.LogError($"[DamageNumberSpawner] Prefab '{PrefabAddress}' 缺少 DamageNumber 组件");
            Object.Destroy(obj);
            return null;
        }
        dn.PrepareForOverlay(poolCanvas);
        return dn;
    }

    private static Transform GetOrCreatePoolRoot()
    {
        // poolRoot == null 同时处理了"从未创建"和"上一场景已销毁"两种情况
        if (poolRoot == null)
        {
            // 旧场景的实例已随场景销毁，pool 中可能有 dead refs，下次 GetOrCreate 会自动跳过
            GameObject root = new GameObject("DamageNumberPool", typeof(RectTransform), typeof(Canvas));
            poolRoot = root.transform;
        }
        EnsurePoolCanvas();
        return poolRoot;
    }

    private static void EnsurePoolCanvas()
    {
        if (poolRoot == null)
            return;

        poolCanvas = poolRoot.GetComponent<Canvas>();
        if (poolCanvas == null)
            poolCanvas = poolRoot.gameObject.AddComponent<Canvas>();

        poolCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        poolCanvas.overrideSorting = true;
        poolCanvas.sortingOrder = short.MaxValue;

        RectTransform rectTransform = poolRoot as RectTransform;
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
