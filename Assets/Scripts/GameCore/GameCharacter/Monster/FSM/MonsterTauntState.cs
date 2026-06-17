using UnityEngine;

public class MonsterTauntState : MonsterState
{
    public MonsterTauntState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Taunt;

    public override void Enter()
    {
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.tauntStateName);
    }

    public override void Tick(float deltaTime)
    {
        if (controller.IsDead())
        {
            controller.ChangeState(MonsterStateType.Dead);
            return;
        }

        if (!controller.HasValidTarget() || controller.IsPlayerLost())
        {
            controller.ChangeState(MonsterStateType.Return);
            return;
        }

        controller.RotateTowards(controller.TargetPlayer.position, deltaTime);

        if (!controller.HasBaseAnimationFinished(controller.tauntStateName))
        {
            return;
        }

        controller.ChangeState(MonsterStateType.Chase);
    }
}
