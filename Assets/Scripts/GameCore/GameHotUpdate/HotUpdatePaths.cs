using System.IO;
using UnityEngine;

/// <summary>
/// 配置热更新的本地可写目录。下载下来的最新配置存在这里，
/// 各 JsonDatabase 读取时优先用这里的文件（编辑器和打包后都生效）。
/// </summary>
public static class HotUpdatePaths
{
    private const string RootFolderName = "HotUpdate";

    /// <summary>热更根目录：persistentDataPath/HotUpdate。</summary>
    public static string Root => Path.Combine(Application.persistentDataPath, RootFolderName);

    /// <summary>本地保存的清单路径。</summary>
    public static string ManifestPath => GetLocalPath("manifest.json");

    /// <summary>把相对路径（如 GameData/ItemData/ConsumableData.json）转成热更目录下的绝对路径。</summary>
    public static string GetLocalPath(string relativePath)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Root, normalized);
    }

    /// <summary>尝试读取热更目录里的文本文件，不存在返回 false。</summary>
    public static bool TryReadText(string relativePath, out string text)
    {
        string fullPath = GetLocalPath(relativePath);
        if (File.Exists(fullPath))
        {
            text = File.ReadAllText(fullPath);
            return true;
        }

        text = null;
        return false;
    }

    /// <summary>把文本写入热更目录（自动创建子目录）。</summary>
    public static void WriteText(string relativePath, string content)
    {
        string fullPath = GetLocalPath(relativePath);
        string dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
    }
}
