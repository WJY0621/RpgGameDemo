using System;
using UnityEngine;

[Serializable]
public class PlayerStatBonus
{
    public int hp;
    public int atk;
    public int def;
    public float atkPercent;
    public float defPercent;
    public float critRate;
    public float critDamage;
    public float moveSpeed;
    public float attackSpeed;

    public void Clear()
    {
        hp = 0;
        atk = 0;
        def = 0;
        atkPercent = 0f;
        defPercent = 0f;
        critRate = 0f;
        critDamage = 0f;
        moveSpeed = 0f;
        attackSpeed = 0f;
    }

    public void Set(
        int hpValue,
        int atkValue,
        int defValue,
        float critRateValue,
        float critDamageValue,
        float moveSpeedValue,
        float attackSpeedValue,
        float atkPercentValue = 0f,
        float defPercentValue = 0f)
    {
        hp = Mathf.Max(0, hpValue);
        atk = Mathf.Max(0, atkValue);
        def = Mathf.Max(0, defValue);
        atkPercent = atkPercentValue;
        defPercent = defPercentValue;
        critRate = critRateValue;
        critDamage = critDamageValue;
        moveSpeed = moveSpeedValue;
        attackSpeed = attackSpeedValue;
    }
}

[Serializable]
public class PlayerData
{
    [Header("Base Stats")]
    public int maxHP;
    public int currentHP;

    public int maxMP;
    public int currentMP;

    public int currentATK;
    public int currentDEF;

    public float currentCritRate;
    public float currentCritDamage;

    public float currentMoveSpeed;
    public float currentAttackSpeed;

    public int GC;

    [Header("Runtime Bonuses")]
    public PlayerStatBonus equipmentBonus = new PlayerStatBonus();
    public PlayerStatBonus buffBonus = new PlayerStatBonus();

    public void EnsureInitialized(PlayerInitialDataSO initialData)
    {
        if (initialData == null)
        {
            ClampCurrentResources();
            return;
        }

        if (maxHP <= 0)
        {
            maxHP = Mathf.Max(1, initialData.HP);
        }

        if (currentHP <= 0)
        {
            currentHP = maxHP;
        }

        if (maxMP <= 0 && initialData.MP > 0)
        {
            maxMP = Mathf.Max(0, initialData.MP);
        }

        if (currentMP <= 0 && maxMP > 0)
        {
            currentMP = maxMP;
        }

        if (currentATK <= 0 && initialData.ATK > 0)
        {
            currentATK = Mathf.Max(0, initialData.ATK);
        }

        if (currentDEF <= 0 && initialData.DEF > 0)
        {
            currentDEF = Mathf.Max(0, initialData.DEF);
        }

        if (currentCritRate <= 0f && initialData.critRate > 0f)
        {
            currentCritRate = initialData.critRate;
        }

        if (currentCritDamage <= 0f && initialData.critDamage > 0f)
        {
            currentCritDamage = initialData.critDamage;
        }

        if (currentMoveSpeed <= 0f && initialData.moveSpeed > 0f)
        {
            currentMoveSpeed = initialData.moveSpeed;
        }

        if (currentAttackSpeed <= 0f && initialData.attackSpeed > 0f)
        {
            currentAttackSpeed = initialData.attackSpeed;
        }

        if (GC <= 0 && initialData.GC > 0)
        {
            GC = Mathf.Max(0, initialData.GC);
        }

        ClampCurrentResources();
    }

    public void ClampCurrentResources()
    {
        currentHP = Mathf.Clamp(currentHP, 0, GetMaxHP());
        currentMP = Mathf.Clamp(currentMP, 0, Mathf.Max(0, maxMP));
    }

    public void ClearRuntimeBonuses()
    {
        equipmentBonus ??= new PlayerStatBonus();
        buffBonus ??= new PlayerStatBonus();
        equipmentBonus.Clear();
        buffBonus.Clear();
        ClampCurrentResources();
    }

    public void ApplyEquipmentStats(
        int hp,
        int atk,
        int def,
        float critRate,
        float critDamage,
        float moveSpeed,
        float attackSpeed,
        float atkPercent = 0f,
        float defPercent = 0f)
    {
        equipmentBonus ??= new PlayerStatBonus();
        equipmentBonus.Set(hp, atk, def, critRate, critDamage, moveSpeed, attackSpeed, atkPercent, defPercent);
        ClampCurrentResources();
    }

    public void ApplyBuffStats(
        int hp,
        int atk,
        int def,
        float critRate,
        float critDamage,
        float moveSpeed,
        float attackSpeed,
        float atkPercent = 0f,
        float defPercent = 0f)
    {
        buffBonus ??= new PlayerStatBonus();
        buffBonus.Set(hp, atk, def, critRate, critDamage, moveSpeed, attackSpeed, atkPercent, defPercent);
        ClampCurrentResources();
    }

    public void ClearBuffStats()
    {
        buffBonus ??= new PlayerStatBonus();
        buffBonus.Clear();
        ClampCurrentResources();
    }

    public int GetMaxHP()
    {
        return Mathf.Max(1, maxHP + GetBonusHP());
    }

    public int GetATK()
    {
        int flatValue = Mathf.Max(0, currentATK + GetBonusATK());
        return Mathf.Max(0, Mathf.RoundToInt(flatValue * (1f + GetBonusATKPercent() / 100f)));
    }

    public int GetDEF()
    {
        int flatValue = Mathf.Max(0, currentDEF + GetBonusDEF());
        return Mathf.Max(0, Mathf.RoundToInt(flatValue * (1f + GetBonusDEFPercent() / 100f)));
    }

    public float GetCritRate()
    {
        return currentCritRate + GetBonusCritRate();
    }

    public float GetCritDamage()
    {
        return currentCritDamage + GetBonusCritDamage();
    }

    public float GetMoveSpeedPercent()
    {
        return currentMoveSpeed + GetBonusMoveSpeed();
    }

    public float GetAttackSpeedPercent()
    {
        return currentAttackSpeed + GetBonusAttackSpeed();
    }

    public float GetMoveSpeedMultiplier()
    {
        return 1f + GetMoveSpeedPercent() / 100f;
    }

    public float GetAttackSpeedMultiplier()
    {
        return 1f + GetAttackSpeedPercent() / 100f;
    }

    public void ModifyHP(int value)
    {
        currentHP = Mathf.Clamp(currentHP + value, 0, GetMaxHP());
    }

    public void ModifyMaxHP(int value)
    {
        maxHP = Mathf.Clamp(maxHP + value, 0, int.MaxValue);
        ClampCurrentResources();
    }

    public void ModifyMP(int value)
    {
        currentMP = Mathf.Clamp(currentMP + value, 0, maxMP);
    }

    public void ModifyMaxMP(int value)
    {
        maxMP = Mathf.Clamp(maxMP + value, 0, int.MaxValue);
        currentMP = Mathf.Clamp(currentMP, 0, maxMP);
    }

    public void ModifyATK(int value)
    {
        currentATK = Mathf.Clamp(currentATK + value, 0, int.MaxValue);
    }

    public void ModifyDEF(int value)
    {
        currentDEF = Mathf.Clamp(currentDEF + value, 0, int.MaxValue);
    }

    public void ModifyGC(int value)
    {
        GC = Mathf.Clamp(GC + value, 0, int.MaxValue);
    }

    public PlayerData CreateSaveSnapshot()
    {
        return new PlayerData
        {
            maxHP = maxHP,
            currentHP = currentHP,
            maxMP = maxMP,
            currentMP = currentMP,
            currentATK = currentATK,
            currentDEF = currentDEF,
            currentCritRate = currentCritRate,
            currentCritDamage = currentCritDamage,
            currentMoveSpeed = currentMoveSpeed,
            currentAttackSpeed = currentAttackSpeed,
            GC = GC,
            equipmentBonus = new PlayerStatBonus(),
            buffBonus = new PlayerStatBonus()
        };
    }

    private int GetBonusHP()
    {
        return (equipmentBonus?.hp ?? 0) + (buffBonus?.hp ?? 0);
    }

    private int GetBonusATK()
    {
        return (equipmentBonus?.atk ?? 0) + (buffBonus?.atk ?? 0);
    }

    private int GetBonusDEF()
    {
        return (equipmentBonus?.def ?? 0) + (buffBonus?.def ?? 0);
    }

    private float GetBonusATKPercent()
    {
        return (equipmentBonus?.atkPercent ?? 0f) + (buffBonus?.atkPercent ?? 0f);
    }

    private float GetBonusDEFPercent()
    {
        return (equipmentBonus?.defPercent ?? 0f) + (buffBonus?.defPercent ?? 0f);
    }

    private float GetBonusCritRate()
    {
        return (equipmentBonus?.critRate ?? 0f) + (buffBonus?.critRate ?? 0f);
    }

    private float GetBonusCritDamage()
    {
        return (equipmentBonus?.critDamage ?? 0f) + (buffBonus?.critDamage ?? 0f);
    }

    private float GetBonusMoveSpeed()
    {
        return (equipmentBonus?.moveSpeed ?? 0f) + (buffBonus?.moveSpeed ?? 0f);
    }

    private float GetBonusAttackSpeed()
    {
        return (equipmentBonus?.attackSpeed ?? 0f) + (buffBonus?.attackSpeed ?? 0f);
    }
}
