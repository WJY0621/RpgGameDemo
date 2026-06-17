using UnityEngine;

public class MonsterHurtState : MonsterState
{
    private float stateElapsedTime;
    private bool hurtAnimationStarted;

    public MonsterHurtState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Hurt;

    public override void Enter()
    {
        stateElapsedTime = 0f;
        hurtAnimationStarted = false;
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.hurtStateName);
    }

    public override void Tick(float deltaTime)
    {
        stateElapsedTime += deltaTime;

        if (controller.IsDead())
        {
            controller.ChangeState(MonsterStateType.Dead);
            return;
        }

        if (controller.HasValidTarget())
        {
            controller.RotateTowards(controller.TargetPlayer.position, deltaTime);
        }

        if (controller.IsCurrentBaseAnimation(controller.hurtStateName))
        {
            hurtAnimationStarted = true;
        }

        if (hurtAnimationStarted)
        {
            if (!controller.HasBaseAnimationFinished(controller.hurtStateName, controller.HurtFinishedNormalizedTime))
            {
                return;
            }
        }
        else if (stateElapsedTime < controller.HurtAnimationStartTimeout)
        {
            return;
        }

        if (!controller.HasValidTarget() || controller.IsPlayerLost())
        {
            controller.ChangeState(MonsterStateType.Return);
            return;
        }

        if (controller.CanEnterAttackState())
        {
            controller.ChangeState(MonsterStateType.Attack);
            return;
        }

        controller.ChangeState(MonsterStateType.Chase);
    }
}
