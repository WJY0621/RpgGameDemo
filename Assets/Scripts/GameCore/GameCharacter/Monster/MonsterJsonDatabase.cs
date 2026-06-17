using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MonsterJsonDatabase
{
    private const string JsonRelativePath = "GameData/MonsterData/MonsterData.json";
    private const string ResourcesPathPrimary = "GameData/MonsterData/MonsterData";
    private const string ResourcesPathFallback = "MonsterData/MonsterData";

    private static readonly Dictionary<int, MonsterData> monsterDataById = new Dictionary<int, MonsterData>();
    private static bool isLoaded;

    public static bool IsLoaded => isLoaded;

    public static void Reload()
    {
        isLoaded = false;
        monsterDataById.Clear();
        EnsureLoaded();
    }

    public static MonsterData GetMonsterData(int monsterID)
    {
        EnsureLoaded();
        monsterDataById.TryGetValue(monsterID, out MonsterData data);
        return data;
    }

    public static MonsterRuntime CreateRuntime(int monsterID)
    {
        MonsterData data = GetMonsterData(monsterID);
        return data != null ? data.ToRuntime() : null;
    }

    private static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        monsterDataById.Clear();

        string json = LoadJsonText();
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[MonsterJsonDatabase] Monster json not found. Expected path: {JsonRelativePath}");
            return;
        }

        MonsterJsonCollection collection = JsonUtility.FromJson<MonsterJsonCollection>(json);
        if (collection?.items == null)
        {
            Debug.LogWarning("[MonsterJsonDatabase] Monster json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.items.Count; i++)
        {
            MonsterData data = collection.items[i];
            if (data == null || data.monsterID <= 0)
            {
                continue;
            }

            monsterDataById[data.monsterID] = data;
        }
    }

    private static string LoadJsonText()
    {
        // 热更目录优先（下载下来的最新配置）。
        if (HotUpdatePaths.TryReadText(JsonRelativePath, out string hotText) && !string.IsNullOrWhiteSpace(hotText))
        {
            return hotText;
        }

        string assetsPath = Path.Combine(Application.dataPath, JsonRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(assetsPath))
        {
            return File.ReadAllText(assetsPath);
        }

        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", JsonRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(projectPath))
        {
            return File.ReadAllText(projectPath);
        }

        TextAsset textAsset = Resources.Load<TextAsset>(ResourcesPathPrimary);
        if (textAsset == null)
        {
            textAsset = Resources.Load<TextAsset>(ResourcesPathFallback);
        }

        return textAsset != null ? textAsset.text : null;
    }

    [Serializable]
    private class MonsterJsonCollection
    {
        public List<MonsterData> items = new List<MonsterData>();
    }
}
