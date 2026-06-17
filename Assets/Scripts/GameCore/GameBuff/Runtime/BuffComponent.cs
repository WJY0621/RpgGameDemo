using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuffComponent : MonoBehaviour
{
    private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>();
    private readonly List<BuffInstance> removalBuffer = new List<BuffInstance>();
    private readonly BuffStatBonus statSumScratch = new BuffStatBonus();

    public IBuffTarget Target { get; private set; }
    public IReadOnlyList<BuffInstance> ActiveBuffs => activeBuffs;

    public event Action<BuffInstance> OnBuffAdded;
    public event Action<BuffInstance> OnBuffRemoved;

    public void Initialize(IBuffTarget target)
    {
        Target = target;
    }

    private void Update()
    {
        if (Target == null || Target.IsDead || activeBuffs.Count == 0)
        {
            return;
        }

        Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        RemoveAll();
    }

    /// <summary>
    /// 施加 buff。同 ID 已存在则刷新持续时间（项目所有 buff 不叠加）。
    /// </summary>
    public BuffInstance Apply(BuffData data, GameObject caster)
    {
        if (data == null || Target == null || Target.IsDead)
        {
            return null;
        }

        BuffInstance existing = FindByID(data.buffID);
        if (existing != null)
        {
            existing.RefreshDuration();
            return existing;
        }

        BuffInstance instance = new BuffInstance(data, caster);
        activeBuffs.Add(instance);

        if (data.IsStatBonus)
        {
            RecalculateStatBonus();
        }

        OnBuffAdded?.Invoke(instance);
        BroadcastAdded(instance);
        return instance;
    }

    public bool Remove(int buffID)
    {
        BuffInstance instance = FindByID(buffID);
        if (instance == null)
        {
            return false;
        }

        return RemoveInstance(instance);
    }

    public void RemoveAll()
    {
        if (activeBuffs.Count == 0)
        {
            statSumScratch.Clear();
            Target?.ApplyTotalBuffStats(statSumScratch);
            return;
        }

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            BuffInstance instance = activeBuffs[i];
            activeBuffs.RemoveAt(i);
            OnBuffRemoved?.Invoke(instance);
            BroadcastRemoved(instance);
        }

        statSumScratch.Clear();
        Target?.ApplyTotalBuffStats(statSumScratch);
    }

    public bool HasBuff(int buffID)
    {
        return FindByID(buffID) != null;
    }

    public BuffInstance GetBuff(int buffID)
    {
        return FindByID(buffID);
    }

    private void Tick(float deltaTime)
    {
        bool needRecalc = false;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            BuffInstance instance = activeBuffs[i];
            instance.TickTime(deltaTime);

            if (instance.Data.IsPeriodic && instance.ConsumeTickIfReady())
            {
                ApplyPeriodicTick(instance);
                if (Target == null || Target.IsDead)
                {
                    return;
                }
            }

            if (instance.IsExpired)
            {
                removalBuffer.Add(instance);
            }
        }

        if (removalBuffer.Count > 0)
        {
            for (int i = 0; i < removalBuffer.Count; i++)
            {
                BuffInstance instance = removalBuffer[i];
                activeBuffs.Remove(instance);
                if (instance.Data.IsStatBonus)
                {
                    needRecalc = true;
                }
                OnBuffRemoved?.Invoke(instance);
                BroadcastRemoved(instance);
            }
            removalBuffer.Clear();
        }

        if (needRecalc)
        {
            RecalculateStatBonus();
        }
    }

    private void ApplyPeriodicTick(BuffInstance instance)
    {
        BuffData data = instance.Data;
        int amount = instance.ConsumeTickAmount(data.value);
        if (amount <= 0)
        {
            // 比如 0.5/Tick 的第一次 tick，整数部分还没攒够 1
            return;
        }

        switch (data.behavior)
        {
            case BuffBehavior.DamageOverTime:
                Target.TakeBuffDamage(amount, instance.Caster);
                break;
            case BuffBehavior.HealOverTime:
                Target.HealBuff(amount);
                break;
        }
    }

    private bool RemoveInstance(BuffInstance instance)
    {
        if (!activeBuffs.Remove(instance))
        {
            return false;
        }

        if (instance.Data.IsStatBonus)
        {
            RecalculateStatBonus();
        }

        OnBuffRemoved?.Invoke(instance);
        BroadcastRemoved(instance);
        return true;
    }

    private void RecalculateStatBonus()
    {
        statSumScratch.Clear();
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            BuffInstance instance = activeBuffs[i];
            BuffData data = instance.Data;
            if (data.IsStatBonus)
            {
                AccumulateStatBonus(statSumScratch, data.behavior, data.value);
            }
        }
        Target?.ApplyTotalBuffStats(statSumScratch);
    }

    private static void AccumulateStatBonus(BuffStatBonus dest, BuffBehavior behavior, float value)
    {
        switch (behavior)
        {
            case BuffBehavior.BoostHP:
                dest.hp += Mathf.RoundToInt(value);
                break;
            case BuffBehavior.BoostATK:
                dest.atk += Mathf.RoundToInt(value);
                break;
            case BuffBehavior.BoostDEF:
                dest.def += Mathf.RoundToInt(value);
                break;
            case BuffBehavior.BoostATKPercent:
                dest.atkPercent += value;
                break;
            case BuffBehavior.BoostDEFPercent:
                dest.defPercent += value;
                break;
            case BuffBehavior.DecreaseATK:
                // 策划填正数表示"降低多少"，运行时减去
                dest.atk -= Mathf.RoundToInt(value);
                break;
            case BuffBehavior.DecreaseDEF:
                dest.def -= Mathf.RoundToInt(value);
                break;
            case BuffBehavior.BoostMoveSpeed:
                dest.moveSpeed += value;
                break;
            case BuffBehavior.BoostAttackSpeed:
                dest.attackSpeed += value;
                break;
        }
    }

    private BuffInstance FindByID(int buffID)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            BuffInstance instance = activeBuffs[i];
            if (instance.Data.buffID == buffID)
            {
                return instance;
            }
        }
        return null;
    }

    private void BroadcastAdded(BuffInstance instance)
    {
        GameMgr.Buff?.RaiseBuffAdded(this, instance);
    }

    private void BroadcastRemoved(BuffInstance instance)
    {
        GameMgr.Buff?.RaiseBuffRemoved(this, instance);
    }
}
