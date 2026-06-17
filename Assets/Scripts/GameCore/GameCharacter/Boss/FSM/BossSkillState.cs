public class BossSkillState : BossState
{
    private float elapsedTime;

    public BossSkillState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Skill;

    public override void Enter()
    {
        elapsedTime = 0f;

        BossSkillSO skill = controller.ActiveSkill;
        if (skill == null)
        {
            controller.ChangeState(BossStateType.Chase);
            return;
        }

        if (skill.stopMovementWhenCasting)
        {
            controller.StopMovement();
        }

        if (controller.HasValidTarget() && skill.faceTargetDuringCast)
        {
            controller.RotateTowards(controller.Target.position, 1f);
        }

        if (controller.SkillRuntimePlayer != null)
        {
            controller.SkillRuntimePlayer.Play(skill);
        }
        else
        {
            controller.PlayBaseAnimation(skill.animationStateName);
        }
    }

    public override void Exit()
    {
        controller.AttackController?.EndAttackWindow();
        controller.SkillRuntimePlayer?.Stop();
    }

    public override void Tick(float deltaTime)
    {
        elapsedTime += deltaTime;

        if (controller.IsDead())
        {
            controller.ChangeState(BossStateType.Dead);
            return;
        }

        BossSkillSO skill = controller.ActiveSkill;
        if (skill == null)
        {
            controller.ChangeState(BossStateType.Chase);
            return;
        }

        if (controller.HasValidTarget() && skill.faceTargetDuringCast)
        {
            controller.RotateTowards(controller.Target.position, deltaTime);
        }

        controller.SkillRuntimePlayer?.Tick(deltaTime);

        bool runtimeFinished = controller.SkillRuntimePlayer != null && controller.SkillRuntimePlayer.IsFinished;
        bool animationFinished = controller.SkillRuntimePlayer == null && controller.HasBaseAnimationFinished(skill.animationStateName, skill.finishedNormalizedTime);
        bool timeout = skill.fallbackDuration > 0f && elapsedTime >= skill.fallbackDuration;
        if (!runtimeFinished && !animationFinished && !timeout)
        {
            return;
        }

        controller.ClearActiveSkill();

        if (!controller.IsBattleActive)
        {
            controller.ChangeState(BossStateType.Inactive);
            return;
        }

        if (!controller.HasValidTarget() || controller.IsTargetLost())
        {
            controller.EndBattle(false);
            return;
        }

        controller.ChangeState(controller.GetDistanceToTarget() > controller.PreferredCombatRange
            ? BossStateType.Chase
            : BossStateType.Idle);
    }
}
