using System.Text;
using UnityEngine;

/// <summary>
/// Buff 文案/数值的统一格式化工具。Editor 预览和运行时 UI（Buff 信息面板/Tooltip）都用这一份。
/// 所有方法都是纯函数，无副作用、无依赖 GameMgr，可以在任何地方调用。
/// </summary>
public static class BuffFormatter
{
    public static string GetBehaviorLabel(BuffBehavior behavior)
    {
        switch (behavior)
        {
            case BuffBehavior.BoostATK: return "提升攻击力";
            case BuffBehavior.BoostDEF: return "提升防御力";
            case BuffBehavior.BoostHP: return "提升生命上限";
            case BuffBehavior.DecreaseATK: return "降低攻击力";
            case BuffBehavior.DecreaseDEF: return "降低防御力";
            case BuffBehavior.BoostMoveSpeed: return "提升移动速度";
            case BuffBehavior.BoostAttackSpeed: return "提升攻击速度";
            case BuffBehavior.DamageOverTime: return "持续伤害";
            case BuffBehavior.HealOverTime: return "持续治疗";
            case BuffBehavior.BoostATKPercent: return "Boost ATK %";
            case BuffBehavior.BoostDEFPercent: return "Boost DEF %";
            default: return behavior.ToString();
        }
    }

    /// <summary>
    /// 该行为的数值字段是否应当作整数对待（仅适合 Editor 输入控件选择，不影响运行时 tick 累积）。
    /// </summary>
    public static bool IsIntBehavior(BuffBehavior behavior)
    {
        switch (behavior)
        {
            case BuffBehavior.BoostATK:
            case BuffBehavior.BoostDEF:
            case BuffBehavior.BoostHP:
            case BuffBehavior.DecreaseATK:
            case BuffBehavior.DecreaseDEF:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 是否是减益类（在显示数值时强制带负号）。
    /// </summary>
    public static bool IsDecreaseBehavior(BuffBehavior behavior)
    {
        return behavior == BuffBehavior.DecreaseATK || behavior == BuffBehavior.DecreaseDEF;
    }

    /// <summary>
    /// "+10" / "2.5/0.5s" / "5/s" / "+5%"
    /// 周期效果会把 value 和 tickInterval 合并成 "X/Ys" 的速率写法。
    /// </summary>
    public static string GetValueText(BuffData data)
    {
        if (data == null) return string.Empty;

        if (data.IsPeriodic)
        {
            if (data.tickInterval <= 0f)
            {
                // 容错：未配 tick 间隔，显示裸值
                return $"{data.value:0.##}";
            }
            if (Mathf.Approximately(data.tickInterval, 1f))
            {
                return $"{data.value:0.##}/s";
            }
            return $"{data.value:0.##}/{data.tickInterval:0.##}s";
        }

        if (IsIntBehavior(data.behavior))
        {
            int v = Mathf.RoundToInt(data.value);
            if (IsDecreaseBehavior(data.behavior))
            {
                // 策划填正数表达"降低多少"，预览统一带负号显示
                return $"-{Mathf.Abs(v)}";
            }
            string sign = v >= 0 ? "+" : string.Empty;
            return $"{sign}{v}";
        }

        string signF = data.value >= 0f ? "+" : string.Empty;
        return $"{signF}{data.value:0.##}%";
    }

    /// <summary>
    /// 把秒数格式化成 "3m / 1m30s / 1h2m / 90s" 等。
    /// includeRawSeconds=true 会在末尾追加原始秒数，便于编辑器核对。
    /// </summary>
    public static string GetDurationText(float seconds, bool includeRawSeconds = false)
    {
        if (seconds <= 0f) return "0s";

        if (seconds < 60f)
        {
            return Mathf.Approximately(seconds % 1f, 0f)
                ? $"{(int)seconds}s"
                : $"{seconds:0.##}s";
        }

        int totalSeconds = Mathf.RoundToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int rem = totalSeconds % 60;

        StringBuilder sb = new StringBuilder();
        if (hours > 0) sb.Append(hours).Append('h');
        if (minutes > 0) sb.Append(minutes).Append('m');
        if (rem > 0) sb.Append(rem).Append('s');
        if (includeRawSeconds) sb.Append("  (").Append(seconds.ToString("0.##")).Append("s)");
        return sb.ToString();
    }

    /// <summary>
    /// 完整一行摘要，比如 "提升攻击力 +10 | 持续 3m" 或 "持续治疗 2.5/0.5s | 持续 30s"。
    /// 周期效果的 tickInterval 已合并进 GetValueText，这里不再重复。
    /// </summary>
    public static string GetSummary(BuffData data, bool includeRawSeconds = false)
    {
        if (data == null) return string.Empty;

        string label = GetBehaviorLabel(data.behavior);
        string value = GetValueText(data);
        string timing = data.IsPermanent ? "永久" : $"持续 {GetDurationText(data.duration, includeRawSeconds)}";
        return $"{label}  {value}  |  {timing}";
    }

    /// <summary>
    /// 运行时倒计时文本，比如 "剩余 12s" 或 "永久"。
    /// </summary>
    public static string GetRemainingText(BuffInstance instance)
    {
        if (instance == null) return string.Empty;
        if (instance.Data.IsPermanent) return "永久";
        return $"剩余 {GetDurationText(Mathf.Max(0f, instance.RemainingTime), false)}";
    }
}
