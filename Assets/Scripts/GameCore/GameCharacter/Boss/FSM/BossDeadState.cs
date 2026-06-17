public class BossDeadState : BossState
{
    public BossDeadState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Dead;

    public override void Enter()
    {
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.DeadStateName);
        controller.EndBattle(true);
    }
}
