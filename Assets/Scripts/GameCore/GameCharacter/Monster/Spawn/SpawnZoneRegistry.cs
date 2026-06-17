using System;
using System.Collections.Generic;

/// <summary>
/// 全局区域注册表（静态）。
/// SpawnZone 在 OnEnable 时自注册，OnDisable 时注销。
/// 玩家进入/离开区域时通过 NotifyEntered/NotifyExited 更新当前激活区域。
/// 多个区域重叠时，按 Priority 取最高优先级；优先级相同时取最先进入的。
/// SpawnManager 通过 CurrentConfig 获取当前应使用的生成配置。
/// </summary>
public static class SpawnZoneRegistry
{
    private static readonly List<SpawnZone> allZones = new List<SpawnZone>();
    private static readonly List<SpawnZone> activeZones = new List<SpawnZone>();

    /// <summary>
    /// 当前激活的最高优先级区域发生变化时触发（参数可能为 null，表示玩家不在任何区域内）。
    /// 订阅者请在 OnDestroy 中取消订阅以避免静态事件持有已销毁对象的引用。
    /// </summary>
    public static event Action<SpawnZoneSO> OnCurrentZoneChanged;

    public static SpawnZone CurrentZone { get; private set; }
    public static SpawnZoneSO CurrentConfig => CurrentZone != null ? CurrentZone.Config : null;

    public static IReadOnlyList<SpawnZone> AllZones => allZones;
    public static IReadOnlyList<SpawnZone> ActiveZones => activeZones;

    public static void Register(SpawnZone zone)
    {
        if (zone == null || allZones.Contains(zone))
            return;
        allZones.Add(zone);
    }

    public static void Unregister(SpawnZone zone)
    {
        if (zone == null)
            return;
        allZones.Remove(zone);
        if (activeZones.Remove(zone))
            UpdateCurrentZone();
    }

    public static void NotifyEntered(SpawnZone zone)
    {
        if (zone == null || activeZones.Contains(zone))
            return;
        activeZones.Add(zone);
        UpdateCurrentZone();
    }

    public static void NotifyExited(SpawnZone zone)
    {
        if (zone == null)
            return;
        if (activeZones.Remove(zone))
            UpdateCurrentZone();
    }

    private static void UpdateCurrentZone()
    {
        SpawnZone newCurrent = null;
        for (int i = 0; i < activeZones.Count; i++)
        {
            SpawnZone zone = activeZones[i];
            if (zone == null)
                continue;
            if (newCurrent == null || zone.Priority > newCurrent.Priority)
                newCurrent = zone;
        }

        if (newCurrent != CurrentZone)
        {
            CurrentZone = newCurrent;
            OnCurrentZoneChanged?.Invoke(CurrentConfig);
        }
    }
}
