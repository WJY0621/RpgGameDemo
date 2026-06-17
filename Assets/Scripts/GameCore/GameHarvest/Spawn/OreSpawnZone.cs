using UnityEngine;

/// <summary>
/// 矿石区域父节点。挂载此组件的 GameObject 是若干 OreSpawnSlot 的父级容器。
/// 子节点 OreSpawnSlot 在 Awake 时通过 GetComponentInParent 自动找到本组件并读取配置。
/// </summary>
[DisallowMultipleComponent]
public class OreSpawnZone : MonoBehaviour
{
    [SerializeField] private OreSpawnZoneSO config;

    public OreSpawnZoneSO Config => config;

    public void SetConfig(OreSpawnZoneSO newConfig)
    {
        config = newConfig;
    }
}
