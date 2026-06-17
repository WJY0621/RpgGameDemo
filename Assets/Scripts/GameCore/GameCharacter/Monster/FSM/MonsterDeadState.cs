public class MonsterDeadState : MonsterState
{
    public MonsterDeadState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Dead;

    public override void Enter()
    {
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.deadStateName);
    }
}
