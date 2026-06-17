public class BossInactiveState : BossState
{
    public BossInactiveState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Inactive;

    public override void Enter()
    {
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.IdleStateName);
    }

    public override void Tick(float deltaTime)
    {
        if (controller.IsDead())
        {
            controller.ChangeState(BossStateType.Dead);
            return;
        }

        if (controller.IsBattleActive)
        {
            controller.ChangeState(BossStateType.Chase);
            return;
        }

        if (controller.AutoStartOnDetect && controller.CanDetectTarget())
        {
            controller.StartBattle();
        }
    }
}
