using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 资源热更新：启动时检查 Addressables 远程目录(catalog)是否有更新，有就更新。
/// 之后通过 Addressables 加载图标/模型时，会自动从服务器下载有变化的 bundle。
/// 远程目录未配置/服务器不可达时是空操作，游戏继续用包内资源。
/// </summary>
public static class ResourceHotUpdater
{
    /// <summary>
    /// 检查并更新 Addressables 远程目录。返回被更新的目录数（0 表示无更新）。
    /// </summary>
    public static async UniTask<int> CheckAndUpdateCatalogsAsync()
    {
        // 确保 Addressables 已初始化（幂等；游戏启动早期通常已初始化过）
        await Addressables.InitializeAsync().ToUniTask();

        // 1. 检查是否有可更新的目录
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        List<string> catalogs = await checkHandle.ToUniTask();
        int count = catalogs != null ? catalogs.Count : 0;

        // 2. 有更新就更新目录（更新后新资源的引用就指向服务器最新 bundle）
        if (count > 0)
        {
            var updateHandle = Addressables.UpdateCatalogs(catalogs, false);
            await updateHandle.ToUniTask();
            Addressables.Release(updateHandle);
        }

        Addressables.Release(checkHandle);
        return count;
    }
}
