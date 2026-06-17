using UnityEngine;

public class BuffInstance
{
    public BuffData Data { get; }
    public GameObject Caster { get; }
    public float RemainingTime { get; private set; }
    public float TickTimer { get; private set; }

    /// <summary>
    /// 小数累积器：DoT/HoT 配 0.5/Tick 时，每 2 个 tick 才会真正扣/回 1 点。
    /// </summary>
    private float tickAccumulator;

    public bool IsExpired => !Data.IsPermanent && RemainingTime <= 0f;

    public BuffInstance(BuffData data, GameObject caster)
    {
        Data = data;
        Caster = caster;
        RemainingTime = data.duration;
        TickTimer = data.tickInterval;
    }

    /// <summary>
    /// 累加本次 tick 的小数数值，返回当前可消耗的整数部分（向 0 截断），剩余的小数留到下次。
    /// </summary>
    public int ConsumeTickAmount(float perTick)
    {
        tickAccumulator += perTick;
        int integerPart = (int)tickAccumulator;
        tickAccumulator -= integerPart;
        return integerPart;
    }

    public void RefreshDuration()
    {
        RemainingTime = Data.duration;
    }

    public void TickTime(float deltaTime)
    {
        if (!Data.IsPermanent)
        {
            RemainingTime -= deltaTime;
        }

        if (Data.tickInterval > 0f)
        {
            TickTimer -= deltaTime;
        }
    }

    public bool ConsumeTickIfReady()
    {
        if (Data.tickInterval <= 0f)
        {
            return false;
        }

        if (TickTimer > 0f)
        {
            return false;
        }

        TickTimer += Data.tickInterval;
        if (TickTimer < 0f)
        {
            TickTimer = Data.tickInterval;
        }

        return true;
    }
}
