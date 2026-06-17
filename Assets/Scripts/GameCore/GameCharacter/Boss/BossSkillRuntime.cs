public sealed class BossSkillRuntime
{
    public BossSkillSO Skill { get; }
    public float CooldownRemaining { get; private set; }
    public bool IsReady => CooldownRemaining <= 0f;

    public BossSkillRuntime(BossSkillSO skill)
    {
        Skill = skill;
    }

    public void Tick(float deltaTime)
    {
        if (CooldownRemaining <= 0f)
        {
            CooldownRemaining = 0f;
            return;
        }

        CooldownRemaining -= deltaTime;
        if (CooldownRemaining < 0f)
        {
            CooldownRemaining = 0f;
        }
    }

    public void StartCooldown(float cooldownMultiplier)
    {
        if (Skill == null)
        {
            CooldownRemaining = 0f;
            return;
        }

        CooldownRemaining = Skill.cooldown * UnityEngine.Mathf.Max(0.01f, cooldownMultiplier);
    }
}
