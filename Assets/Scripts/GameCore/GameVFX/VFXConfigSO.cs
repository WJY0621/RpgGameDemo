using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特效配置总表 ScriptableObject。
/// 在 Addressables 中以 "VFXConfig" 为地址注册。
/// 每条 VFXEntry 对应一个特效：key 是代码中使用的名字，address 是 Addressables 地址。
/// </summary>
[CreateAssetMenu(fileName = "VFXConfig", menuName = "Data/GameVFX/VFX Config")]
public class VFXConfigSO : ScriptableObject
{
    public List<VFXEntry> entries = new List<VFXEntry>();

    private Dictionary<string, VFXEntry> entryDict;

    /// <summary>按 key 查找配置项，首次调用时构建缓存字典。</summary>
    public VFXEntry GetEntry(string key)
    {
        if (entryDict == null)
        {
            BuildDict();
        }

        entryDict.TryGetValue(key, out VFXEntry entry);
        return entry;
    }

    private void BuildDict()
    {
        entryDict = new Dictionary<string, VFXEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            VFXEntry e = entries[i];
            if (!string.IsNullOrWhiteSpace(e.key))
            {
                entryDict[e.key] = e;
            }
        }
    }

    // 在 Editor 修改配置后重置缓存
    private void OnValidate()
    {
        entryDict = null;
    }
}

[Serializable]
public class VFXEntry
{
    [Tooltip("代码中使用的唯一标识符，对应 VFXKeys 中的常量")]
    public string key;

    [Tooltip("Addressables 中该特效预制体的地址")]
    public string address;

    [Min(0), Tooltip("游戏启动时预先创建的对象池实例数量")]
    public int preloadCount = 3;
}
