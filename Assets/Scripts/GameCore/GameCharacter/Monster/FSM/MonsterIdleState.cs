using UnityEngine;

public class MonsterIdleState : MonsterState
{
    public MonsterIdleState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Idle;

    public override void Enter()
    {
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.idleStateName);
    }

    public override void Tick(float deltaTime)
    {
        if (controller.IsDead())
        {
            controller.ChangeState(MonsterStateType.Dead);
            return;
        }

        if (controller.CanDetectPlayer())
        {
            controller.ChangeState(MonsterStateType.Taunt);
            return;
        }

        if (controller.UsePatrol())
        {
            controller.ChangeState(MonsterStateType.Patrol);
        }
    }
}
