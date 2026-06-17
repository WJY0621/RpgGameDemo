using UnityEngine;

public class MonsterPatrolState : MonsterState
{
    private float waitTimer;

    public MonsterPatrolState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Patrol;

    public override void Enter()
    {
        waitTimer = 0f;
        controller.PlayBaseAnimation(controller.walkStateName);
    }

    public override void Tick(float deltaTime)
    {
        if (controller.IsDead())
        {
            controller.ChangeState(MonsterStateType.Dead);
            return;
        }

        if (!controller.UsePatrol())
        {
            controller.ChangeState(MonsterStateType.Idle);
            return;
        }

        if (controller.CanDetectPlayer())
        {
            controller.ChangeState(MonsterStateType.Taunt);
            return;
        }

        Transform patrolPoint = controller.GetCurrentPatrolPoint();
        if (patrolPoint == null)
        {
            controller.ChangeState(MonsterStateType.Idle);
            return;
        }

        controller.MoveTowards(patrolPoint.position, deltaTime);
        if (!controller.ReachedPosition(patrolPoint.position))
        {
            return;
        }

        waitTimer += deltaTime;
        if (waitTimer >= controller.patrolWaitDuration)
        {
            waitTimer = 0f;
            controller.AdvancePatrolPoint();
        }
    }
}
