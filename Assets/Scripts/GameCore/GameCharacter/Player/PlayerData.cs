using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData
{
    public int maxHP;
    public int currentHP;

    public int maxMP;
    public int currentMP;

    public int additionalATK;//装备或其它物品所增加的攻击力
    public int currentATK;//角色本身的攻击力

    public int additionalDEF;//装备或其它物品所增加的防御力
    public int currentDEF;//角色本身的防御力

    public int GC;//游戏钱币

    public void ModifyHP(int value)
    {
        currentHP = Mathf.Clamp(currentHP + value, 0, maxHP);
        if (currentHP == 0)
        {
            //TODO: 角色死亡处理
        }
    }

    public void ModifyMaxHP(int value)
    {
        maxHP = Mathf.Clamp(maxHP + value, 0, int.MaxValue);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
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

    public void ModifyAdditionalATK(int value)
    {
        additionalATK = Mathf.Clamp(additionalATK + value, 0, int.MaxValue);
    }
    public void ModifyAdditionalDEF(int value)
    {
        additionalDEF = Mathf.Clamp(additionalDEF + value, 0, int.MaxValue);
    }

    public int GetATK()
    {
        return currentATK + additionalATK;
    }

    public int GetDEF()
    {
        return currentDEF + additionalDEF;
    }
}
