using UnityEngine;

public class MonsterChaseState : MonsterState
{
    public MonsterChaseState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Chase;

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

        if (!controller.HasValidTarget())
        {
            controller.ChangeState(MonsterStateType.Return);
            return;
        }

        if (controller.IsPlayerLost())
        {
            controller.ChangeState(MonsterStateType.Return);
            return;
        }

        if (controller.CanEnterAttackState())
        {
            controller.ChangeState(MonsterStateType.Attack);
            return;
        }

        controller.MoveTowards(controller.TargetPlayer.position, deltaTime);
    }
}
