using UnityEngine;

/// <summary>
/// 把不同的角色（玩家 / 怪物）适配成 BuffComponent 能统一调用的接口。
/// </summary>
public interface IBuffTarget
{
    GameObject GameObject { get; }
    bool IsDead { get; }

    /// <summary>
    /// 用 BuffComponent 汇总后的总属性加成覆盖目标的 buff 通道。
    /// </summary>
    void ApplyTotalBuffStats(BuffStatBonus totalBonus);

    /// <summary>
    /// DoT：让目标承受一次 buff 来源的伤害。caster 可能为 null。
    /// </summary>
    void TakeBuffDamage(int amount, GameObject caster);

    /// <summary>
    /// HoT：给目标治疗一定数值。
    /// </summary>
    void HealBuff(int amount);
}
