using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 配置热更新下载器。启动时从 WorkDemoServer 拉取清单（manifest），
/// 对比本地已应用的清单，把有变化的 JSON 下载到 <see cref="HotUpdatePaths"/> 目录。
/// 失败（如服务器未开）时静默跳过，游戏继续用包内基线配置。
/// </summary>
public static class ConfigHotUpdater
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const int RequestTimeoutSeconds = 8;

    [Serializable]
    private class HotUpdateManifest
    {
        public List<HotUpdateFileEntry> files = new List<HotUpdateFileEntry>();
    }

    [Serializable]
    private class HotUpdateFileEntry
    {
        public string path;
        public string hash;
    }

    /// <summary>
    /// 检查并应用配置热更。返回实际更新的文件数（0 表示无更新或服务器不可用）。
    /// </summary>
    public static async UniTask<int> CheckAndApplyAsync()
    {
        string baseUrl = ResolveBaseUrl();

        // 1. 拉取远程清单
        string remoteManifestJson = await GetTextAsync($"{baseUrl}/api/hotupdate/manifest");
        if (string.IsNullOrWhiteSpace(remoteManifestJson))
        {
            return 0; // 服务器不可达或无内容 → 用包内基线
        }

        HotUpdateManifest remoteManifest = ParseManifest(remoteManifestJson);
        if (remoteManifest == null || remoteManifest.files == null || remoteManifest.files.Count == 0)
        {
            return 0;
        }

        // 2. 读取本地已应用的清单，建立 path -> hash 索引
        Dictionary<string, string> localHashByPath = BuildLocalHashIndex();

        // 3. 下载有变化（或本地缺失）的文件
        int updated = 0;
        bool anyFailed = false;
        for (int i = 0; i < remoteManifest.files.Count; i++)
        {
            HotUpdateFileEntry entry = remoteManifest.files[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.path))
            {
                continue;
            }

            localHashByPath.TryGetValue(entry.path, out string localHash);
            if (string.Equals(localHash, entry.hash, StringComparison.OrdinalIgnoreCase))
            {
                continue; // 没变化
            }

            string fileUrl = $"{baseUrl}/api/hotupdate/file?path={UnityWebRequest.EscapeURL(entry.path)}";
            string content = await GetTextAsync(fileUrl);
            if (content == null)
            {
                anyFailed = true;
                continue;
            }

            HotUpdatePaths.WriteText(entry.path, content);
            updated++;
        }

        // 4. 全部成功才覆盖本地清单（有失败则保留旧清单，下次重试缺失项）
        if (!anyFailed)
        {
            HotUpdatePaths.WriteText("manifest.json", remoteManifestJson);
        }

        return updated;
    }

    private static Dictionary<string, string> BuildLocalHashIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!HotUpdatePaths.TryReadText("manifest.json", out string localJson) || string.IsNullOrWhiteSpace(localJson))
        {
            return index;
        }

        HotUpdateManifest local = ParseManifest(localJson);
        if (local?.files == null)
        {
            return index;
        }

        for (int i = 0; i < local.files.Count; i++)
        {
            HotUpdateFileEntry entry = local.files[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.path))
            {
                index[entry.path] = entry.hash;
            }
        }

        return index;
    }

    private static HotUpdateManifest ParseManifest(string json)
    {
        try
        {
            return JsonUtility.FromJson<HotUpdateManifest>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ConfigHotUpdater] 解析清单失败：{ex.Message}");
            return null;
        }
    }

    private static async UniTask<string> GetTextAsync(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = RequestTimeoutSeconds;
            try
            {
                await request.SendWebRequest().ToUniTask();
            }
            catch (Exception)
            {
                return null; // 连接失败/超时 → 视为无更新
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            return request.downloadHandler.text;
        }
    }

    private static string ResolveBaseUrl()
    {
        string saved = PlayerPrefs.GetString(BaseUrlKey, string.Empty);
        return string.IsNullOrWhiteSpace(saved) ? DefaultBaseUrl : saved.Trim().TrimEnd('/');
    }
}
