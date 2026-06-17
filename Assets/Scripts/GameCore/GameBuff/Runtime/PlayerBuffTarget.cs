using UnityEngine;

public class PlayerBuffTarget : IBuffTarget
{
    private readonly PlayerHealth health;

    public PlayerBuffTarget(PlayerHealth health)
    {
        this.health = health;
    }

    public GameObject GameObject => health != null ? health.gameObject : null;

    public bool IsDead => health == null || health.IsDead;

    public void ApplyTotalBuffStats(BuffStatBonus totalBonus)
    {
        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        if (data == null)
        {
            return;
        }

        if (totalBonus == null)
        {
            data.ApplyBuffStats(0, 0, 0, 0f, 0f, 0f, 0f);
        }
        else
        {
            data.ApplyBuffStats(
                totalBonus.hp,
                totalBonus.atk,
                totalBonus.def,
                totalBonus.critRate,
                totalBonus.critDamage,
                totalBonus.moveSpeed,
                totalBonus.attackSpeed,
                totalBonus.atkPercent,
                totalBonus.defPercent);
        }

        if (health != null)
        {
            health.RefreshStatsFromPlayerData();
        }
    }

    public void TakeBuffDamage(int amount, GameObject caster)
    {
        if (health == null || amount <= 0)
        {
            return;
        }

        AttackDamageInfo info = new AttackDamageInfo(
            amount,
            health.transform.position + Vector3.up * 1.2f,
            Vector3.zero,
            caster);
        health.TakeDamage(info);
    }

    public void HealBuff(int amount)
    {
        if (health == null || amount <= 0)
        {
            return;
        }

        health.Heal(amount);
    }
}
