public class BossIdleState : BossState
{
    private float decisionTimer;

    public BossIdleState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Idle;

    public override void Enter()
    {
        decisionTimer = 0f;
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.IdleBattleStateName);
    }

    public override void Tick(float deltaTime)
    {
        if (controller.IsDead())
        {
            controller.ChangeState(BossStateType.Dead);
            return;
        }

        if (!controller.IsBattleActive)
        {
            controller.ChangeState(BossStateType.Inactive);
            return;
        }

        if (!controller.HasValidTarget())
        {
            controller.CachePlayerTarget();
            return;
        }

        if (controller.IsTargetLost())
        {
            controller.EndBattle(false);
            return;
        }

        controller.RotateTowards(controller.Target.position, deltaTime);

        decisionTimer -= deltaTime;
        if (decisionTimer > 0f)
        {
            return;
        }

        decisionTimer = controller.SkillDecisionInterval;
        if (controller.TryStartBestSkill())
        {
            return;
        }

        if (controller.CanMove && controller.GetDistanceToTarget() > controller.PreferredCombatRange)
        {
            controller.ChangeState(BossStateType.Chase);
        }
    }
}
