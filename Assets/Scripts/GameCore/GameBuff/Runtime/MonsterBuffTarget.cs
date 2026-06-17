using UnityEngine;

public class MonsterBuffTarget : IBuffTarget
{
    private readonly MonsterController controller;
    private readonly MonsterHealth health;

    public MonsterBuffTarget(MonsterController controller, MonsterHealth health)
    {
        this.controller = controller;
        this.health = health;
    }

    public GameObject GameObject => controller != null ? controller.gameObject : (health != null ? health.gameObject : null);

    public bool IsDead => health == null || health.IsDead;

    public void ApplyTotalBuffStats(BuffStatBonus totalBonus)
    {
        if (controller == null || controller.Runtime == null)
        {
            return;
        }

        controller.Runtime.ApplyBuffStats(totalBonus);

        if (health != null)
        {
            health.RefreshStatsFromRuntime();
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
