using UnityEngine;

public class BossAnimationEventRelay : MonoBehaviour
{
    private BossAttackController attackController;
    private BossController bossController;

    public void Bind(BossController controller, BossAttackController attack)
    {
        bossController = controller;
        attackController = attack;
    }

    public void BeginAttackWindow()
    {
        attackController?.BeginAttackWindow();
    }

    public void ApplyAttackDamage()
    {
        attackController?.ApplyAttackDamage();
    }

    public void EndAttackWindow()
    {
        attackController?.EndAttackWindow();
    }

    public void BossSkillEvent(int eventIndex)
    {
        bossController?.HandleSkillAnimationEvent(eventIndex);
    }

    public void BossSkillAudioEvent(int eventIndex)
    {
        bossController?.SkillRuntimePlayer?.TriggerAudioEventByAnimation(eventIndex);
    }

    public void BossSkillBarrageEvent(int eventIndex)
    {
        bossController?.SkillRuntimePlayer?.TriggerBarrageEventByAnimation(eventIndex);
    }

    public void BossSkillHitboxBegin(int eventIndex)
    {
        bossController?.SkillRuntimePlayer?.BeginHitboxEventByAnimation(eventIndex);
    }

    public void BossSkillHitboxApply(int eventIndex)
    {
        bossController?.SkillRuntimePlayer?.ApplyHitboxEventByAnimation(eventIndex);
    }

    public void BossSkillHitboxEnd(int eventIndex)
    {
        bossController?.SkillRuntimePlayer?.EndHitboxEventByAnimation(eventIndex);
    }
}
