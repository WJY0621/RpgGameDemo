using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public class MonsterDropItemRuntime
{
    public int itemID;
    public float dropChance;
    public int amount;

    public MonsterDropItemRuntime()
    {
    }

    public MonsterDropItemRuntime(MonsterDropItemData data)
    {
        if (data == null)
        {
            return;
        }

        itemID = data.itemID;
        dropChance = Mathf.Clamp(data.dropChance, 0f, 100f);
        amount = Mathf.Max(1, data.amount);
    }
}

[Serializable]
public class MonsterRuntime
{
    [Header("Basic Info")]
    public int monsterID;
    public string monsterName;
    public string modelName;

    [Header("Runtime Stats")]
    public int maxHP;
    public int currentHP;
    public int currentATK;
    public int currentDEF;
    public float moveSpeed;

    [Header("AI Ranges")]
    public float alertRange;
    public float chaseRange;
    public float attackRange;
    public float loseTargetRange;

    [Header("Drops")]
    public string goldDrop;
    public int goldDropMin;
    public int goldDropMax;
    public List<MonsterDropItemRuntime> dropItems = new List<MonsterDropItemRuntime>();

    [Header("Runtime Bonuses")]
    public BuffStatBonus buffBonus = new BuffStatBonus();

    public bool isDead;

    public MonsterRuntime()
    {
    }

    public MonsterRuntime(MonsterData data)
    {
        ApplyData(data);
    }

    public void ApplyData(MonsterData data)
    {
        if (data == null)
        {
            return;
        }

        monsterID = data.monsterID;
        monsterName = data.monsterName;
        modelName = data.modelName;

        maxHP = Mathf.Max(1, data.HP);
        currentHP = maxHP;
        currentATK = Mathf.Max(0, data.ATK);
        currentDEF = Mathf.Max(0, data.DEF);
        moveSpeed = Mathf.Max(0f, data.moveSpeed);

        alertRange = Mathf.Max(0f, data.alertRange);
        chaseRange = Mathf.Max(0f, data.chaseRange);
        attackRange = Mathf.Max(0f, data.attackRange);
        loseTargetRange = Mathf.Max(0f, data.loseTargetRange);

        goldDrop = data.goldDrop?.Trim() ?? string.Empty;
        ParseGoldDropRange(goldDrop, out goldDropMin, out goldDropMax);

        dropItems = new List<MonsterDropItemRuntime>();
        if (data.dropItems != null)
        {
            for (int i = 0; i < data.dropItems.Count; i++)
            {
                MonsterDropItemData dropData = data.dropItems[i];
                if (dropData == null || dropData.itemID <= 0)
                {
                    continue;
                }

                dropItems.Add(new MonsterDropItemRuntime(dropData));
            }
        }

        isDead = false;
    }

    private static void ParseGoldDropRange(string raw, out int min, out int max)
    {
        min = 0;
        max = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        string normalized = raw.Trim();
        string[] parts = normalized.Split(new[] { '_', '~', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        if (parts.Length == 1)
        {
            if (int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int single))
            {
                min = Mathf.Max(0, single);
                max = min;
            }
            return;
        }

        int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out min);
        int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out max);
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
    }

    public int RollGoldDrop()
    {
        if (goldDropMax <= 0)
        {
            return 0;
        }

        if (goldDropMax <= goldDropMin)
        {
            return goldDropMin;
        }

        return UnityEngine.Random.Range(goldDropMin, goldDropMax + 1);
    }

    public List<MonsterDropItemRuntime> RollItemDrops()
    {
        List<MonsterDropItemRuntime> result = new List<MonsterDropItemRuntime>();
        if (dropItems == null || dropItems.Count == 0)
        {
            return result;
        }

        for (int i = 0; i < dropItems.Count; i++)
        {
            MonsterDropItemRuntime drop = dropItems[i];
            if (drop == null || drop.itemID <= 0 || drop.amount <= 0)
            {
                continue;
            }

            if (UnityEngine.Random.value * 100f <= Mathf.Clamp(drop.dropChance, 0f, 100f))
            {
                result.Add(drop);
            }
        }

        return result;
    }

    public void ModifyHP(int value)
    {
        currentHP = Mathf.Clamp(currentHP + value, 0, GetMaxHP());
        isDead = currentHP <= 0;
    }

    public void ModifyATK(int value)
    {
        currentATK = Mathf.Clamp(currentATK + value, 0, int.MaxValue);
    }

    public void ModifyDEF(int value)
    {
        currentDEF = Mathf.Clamp(currentDEF + value, 0, int.MaxValue);
    }

    public float GetNormalizedHP()
    {
        int max = GetMaxHP();
        return max <= 0 ? 0f : (float)currentHP / max;
    }

    public int GetMaxHP()
    {
        return Mathf.Max(1, maxHP + (buffBonus != null ? buffBonus.hp : 0));
    }

    public int GetATK()
    {
        int flatValue = Mathf.Max(0, currentATK + (buffBonus != null ? buffBonus.atk : 0));
        float percent = buffBonus != null ? buffBonus.atkPercent : 0f;
        return Mathf.Max(0, Mathf.RoundToInt(flatValue * (1f + percent / 100f)));
    }

    public int GetDEF()
    {
        int flatValue = Mathf.Max(0, currentDEF + (buffBonus != null ? buffBonus.def : 0));
        float percent = buffBonus != null ? buffBonus.defPercent : 0f;
        return Mathf.Max(0, Mathf.RoundToInt(flatValue * (1f + percent / 100f)));
    }

    public float GetMoveSpeedMultiplier()
    {
        float bonusPercent = buffBonus != null ? buffBonus.moveSpeed : 0f;
        return 1f + bonusPercent / 100f;
    }

    public void ApplyBuffStats(BuffStatBonus totalBonus)
    {
        buffBonus ??= new BuffStatBonus();
        if (totalBonus == null)
        {
            buffBonus.Clear();
        }
        else
        {
            buffBonus.hp = totalBonus.hp;
            buffBonus.atk = totalBonus.atk;
            buffBonus.def = totalBonus.def;
            buffBonus.atkPercent = totalBonus.atkPercent;
            buffBonus.defPercent = totalBonus.defPercent;
            buffBonus.critRate = totalBonus.critRate;
            buffBonus.critDamage = totalBonus.critDamage;
            buffBonus.moveSpeed = totalBonus.moveSpeed;
            buffBonus.attackSpeed = totalBonus.attackSpeed;
        }

        currentHP = Mathf.Clamp(currentHP, 0, GetMaxHP());
    }
}
