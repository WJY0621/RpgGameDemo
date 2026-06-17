using UnityEngine;

public class MonsterReturnState : MonsterState
{
    public MonsterReturnState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Return;

    public override void Enter()
    {
        controller.PlayBaseAnimation(controller.runStateName);
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

        controller.MoveTowards(controller.SpawnPosition, deltaTime);
        if (!controller.ReachedPosition(controller.SpawnPosition))
        {
            return;
        }

        controller.ChangeState(controller.UsePatrol() ? MonsterStateType.Patrol : MonsterStateType.Idle);
    }
}
