using System;

[Serializable]
public class BuffStatBonus
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

    public bool IsZero =>
        hp == 0 && atk == 0 && def == 0 &&
        atkPercent == 0f && defPercent == 0f &&
        critRate == 0f && critDamage == 0f &&
        moveSpeed == 0f && attackSpeed == 0f;

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

    public void AddScaled(BuffStatBonus other, int multiplier)
    {
        if (other == null || multiplier == 0)
        {
            return;
        }

        hp += other.hp * multiplier;
        atk += other.atk * multiplier;
        def += other.def * multiplier;
        atkPercent += other.atkPercent * multiplier;
        defPercent += other.defPercent * multiplier;
        critRate += other.critRate * multiplier;
        critDamage += other.critDamage * multiplier;
        moveSpeed += other.moveSpeed * multiplier;
        attackSpeed += other.attackSpeed * multiplier;
    }
}
