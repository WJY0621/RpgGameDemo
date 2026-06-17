public class BossChaseState : BossState
{
    private float decisionTimer;

    public BossChaseState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Chase;

    public override void Enter()
    {
        decisionTimer = 0f;
        controller.PlayBaseAnimation(controller.CanMove ? controller.RunStateName : controller.IdleBattleStateName);
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

        decisionTimer -= deltaTime;
        if (decisionTimer <= 0f)
        {
            decisionTimer = controller.SkillDecisionInterval;
            if (controller.TryStartBestSkill())
            {
                return;
            }
        }

        if (controller.GetDistanceToTarget() <= controller.PreferredCombatRange)
        {
            controller.ChangeState(BossStateType.Idle);
            return;
        }

        if (!controller.CanMove)
        {
            controller.ChangeState(BossStateType.Idle);
            return;
        }

        controller.MoveTowards(controller.Target.position);
    }
}
