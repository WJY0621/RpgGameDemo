using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

//资源加载的信息 用于存储于字典中
public class AddressablesInfo
{
    public AsyncOperationHandle handle;
    public uint count;

    public AddressablesInfo(AsyncOperationHandle handle)
    {
        this.handle = handle;
        count += 1;
    }
}
public class AssetLoader
{
    //将加载资源的权柄存入字典中
    public Dictionary<string, AddressablesInfo> loadedAssetDic = new Dictionary<string, AddressablesInfo>();
    public Dictionary<string, GameObject> loadedPrefabDic = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, UniTaskCompletionSource<GameObject>> loadingPrefabTasks =
        new Dictionary<string, UniTaskCompletionSource<GameObject>>();

    /// <summary>
    /// 加载场景
    /// </summary>
    /// <param name="sceneName">场景名字</param>
    /// <param name="progressCallBack">加载进度回调</param>
    /// <param name="loadSuccessCallBack">加载完成回调</param>
    /// <returns></returns>
    public async UniTask loadScene(string sceneName, Action<float> progressCallBack, Action<SceneInstance> loadSuccessCallBack)
    {
        //创建异步加载 且加载完成后不会立即执行
        AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(sceneName, activateOnLoad: false);
        //进度回调
        var progress = progressCallBack != null ? Progress.Create<float>(progressCallBack) : null;
        //等待加载完成 且传递进度
        await handle.ToUniTask(progress);
        //执行场景加载完成的回调函数
        loadSuccessCallBack?.Invoke(handle.Result);
    }

    public async UniTask<GameObject> LoadPrefab(string prefabName, Action<GameObject> LoadSuccessCallBack = null)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            Debug.LogWarning("[AssetLoader] LoadPrefab called with empty prefab name.");
            return null;
        }

        if (loadedPrefabDic.TryGetValue(prefabName, out GameObject cachedPrefab))
        {
            LoadSuccessCallBack?.Invoke(cachedPrefab);
            return cachedPrefab;
        }

        if (loadingPrefabTasks.TryGetValue(prefabName, out UniTaskCompletionSource<GameObject> pendingTask))
        {
            GameObject pendingPrefab = await pendingTask.Task;
            LoadSuccessCallBack?.Invoke(pendingPrefab);
            return pendingPrefab;
        }

        UniTaskCompletionSource<GameObject> loadingTask = new UniTaskCompletionSource<GameObject>();
        loadingPrefabTasks[prefabName] = loadingTask;

        try
        {
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(prefabName);
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                loadedPrefabDic[prefabName] = handle.Result;
                loadingTask.TrySetResult(handle.Result);
                LoadSuccessCallBack?.Invoke(handle.Result);
                return handle.Result;
            }

            Debug.LogError($"[AssetLoader] LoadPrefab Failed: {prefabName}, Status: {handle.Status}");
            loadingTask.TrySetResult(null);
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AssetLoader] Exception in LoadPrefab({prefabName}): {e}");
            loadingTask.TrySetResult(null);
            return null;
        }
        finally
        {
            if (loadingPrefabTasks.TryGetValue(prefabName, out UniTaskCompletionSource<GameObject> task) &&
                task == loadingTask)
            {
                loadingPrefabTasks.Remove(prefabName);
            }
        }
    }

    // 记录正在加载的任务，防止重复加载导致的 Race Condition
    private Dictionary<string, UniTask> loadingTasks = new Dictionary<string, UniTask>();

    /// <summary>
    /// 加载资源
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="name">资源名字</param>
    /// <param name="LoadSuccessCallBack">资源加载完成的回调函数</param>
    /// <returns></returns>
    public async UniTask<T> LoadAsset<T>(string name, Action<AsyncOperationHandle<T>> LoadSuccessCallBack = null)
    {
        // 存入字典的键格式为资源名 加 资源类型名
        string keyName = name + "_" + typeof(T).Name;

        // 1. 如果已经加载过，直接返回
        if (loadedAssetDic.ContainsKey(keyName))
        {
            var cachedHandle = loadedAssetDic[keyName].handle.Convert<T>();
            loadedAssetDic[keyName].count += 1;
            LoadSuccessCallBack?.Invoke(cachedHandle);
            return cachedHandle.Result;
        }

        // 2. 如果正在加载中，等待之前的任务完成
        if (loadingTasks.ContainsKey(keyName))
        {
            await loadingTasks[keyName];
            // 递归调用，此时应该已经进入已加载的分支
            return await LoadAsset<T>(name, LoadSuccessCallBack);
        }

        // 3. 开始新的加载任务
        var tcs = new UniTaskCompletionSource<T>();
        loadingTasks[keyName] = tcs.Task;

        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(name);
            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                AddressablesInfo info = new AddressablesInfo(handle);
                loadedAssetDic[keyName] = info;
                loadedAssetDic[keyName].count += 1;
                LoadSuccessCallBack?.Invoke(handle);
                tcs.TrySetResult(handle.Result);
                return handle.Result;
            }
            else
            {
                Debug.LogError($"[AssetLoader] Failed to load asset: '{name}' of type {typeof(T).Name}. Status: {handle.Status}");
                tcs.TrySetResult(default);
                return default;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AssetLoader] Exception while loading asset '{name}': {e.Message}");
            tcs.TrySetResult(default);
            return default;
        }
        finally
        {
            // 无论成功失败，移除正在加载的任务标记
            loadingTasks.Remove(keyName);
        }
    }

    public void Release<T>(string name)
    {
        string keyName = name + '_' + typeof(T).Name;
        if (loadedAssetDic.ContainsKey(keyName))
        {
            loadedAssetDic[keyName].count -= 1;
            if (loadedAssetDic[keyName].count == 0)
            {
                AsyncOperationHandle<T> handle = loadedAssetDic[keyName].handle.Convert<T>();
                Addressables.Release(handle);
                loadedAssetDic.Remove(keyName);
            }
        }
    }

    public void Clear()
    {
        // 分步清理，避免一次性释放大量资源
        if (loadedAssetDic.Count > 0)
        {
            var keysToRemove = new List<string>(loadedAssetDic.Keys);
            foreach (var key in keysToRemove)
            {
                if (loadedAssetDic.TryGetValue(key, out var info))
                {
                    Addressables.Release(info.handle);
                }
            }
            loadedAssetDic.Clear();
        }

        // 清理预制体缓存
        if (loadedPrefabDic.Count > 0)
        {
            var prefabKeys = new List<string>(loadedPrefabDic.Keys);
            foreach (var key in prefabKeys)
            {
                if (loadedPrefabDic.TryGetValue(key, out var prefab) && prefab != null)
                {
                    Addressables.Release(prefab);
                }
            }
            loadedPrefabDic.Clear();
        }

        // 使用异步方式清理未使用的资源，避免同步阻塞
        Resources.UnloadUnusedAssets();
    }
    
    /// <summary>
    /// 异步清理资源（推荐使用）
    /// </summary>
    public async UniTask ClearAsync()
    {
        // 分步清理，避免一次性释放大量资源
        if (loadedAssetDic.Count > 0)
        {
            var keysToRemove = new List<string>(loadedAssetDic.Keys);
            foreach (var key in keysToRemove)
            {
                if (loadedAssetDic.TryGetValue(key, out var info))
                {
                    Addressables.Release(info.handle);
                    await UniTask.Yield(); // 每释放一个资源后让出一帧
                }
            }
            loadedAssetDic.Clear();
        }

        // 清理预制体缓存
        if (loadedPrefabDic.Count > 0)
        {
            var prefabKeys = new List<string>(loadedPrefabDic.Keys);
            foreach (var key in prefabKeys)
            {
                if (loadedPrefabDic.TryGetValue(key, out var prefab) && prefab != null)
                {
                    Addressables.Release(prefab);
                    await UniTask.Yield();
                }
            }
            loadedPrefabDic.Clear();
        }

        // 异步清理未使用的资源
        var unloadOperation = Resources.UnloadUnusedAssets();
        await unloadOperation;
    }
}
