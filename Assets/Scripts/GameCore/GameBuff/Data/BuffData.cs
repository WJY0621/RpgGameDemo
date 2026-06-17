using System;
using UnityEngine;

[Serializable]
public class BuffData
{
    [Header("Basic")]
    public int buffID;
    public string buffName;
    public string iconName;
    [TextArea(2, 4)] public string description;

    [Header("Behavior")]
    public BuffBehavior behavior = BuffBehavior.BoostATK;
    [Tooltip("数值含义随 behavior 变化：整数加成或百分比")]
    public float value;

    [Header("Timing")]
    [Tooltip("持续秒数；<=0 表示永久（需手动移除）")]
    public float duration;
    [Tooltip("DoT/HoT 触发间隔秒；其他类型忽略")]
    public float tickInterval;

    public bool IsPermanent => duration <= 0f;

    public bool IsPeriodic =>
        behavior == BuffBehavior.DamageOverTime ||
        behavior == BuffBehavior.HealOverTime;

    public bool IsStatBonus => !IsPeriodic;
}
