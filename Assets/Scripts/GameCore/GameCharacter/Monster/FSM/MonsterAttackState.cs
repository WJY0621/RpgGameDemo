using UnityEngine;

public class MonsterAttackState : MonsterState
{
    private float stateElapsedTime;
    private bool attackInProgress;
    private bool isInBattleIdle;

    public MonsterAttackState(MonsterController controller) : base(controller)
    {
    }

    public override MonsterStateType StateType => MonsterStateType.Attack;

    public override void Enter()
    {
        stateElapsedTime = 0f;
        attackInProgress = false;
        isInBattleIdle = false;

        controller.StopMovement();

        if (controller.IsAttackCooldownReady())
        {
            StartAttack();
            return;
        }

        PlayBattleIdle();
    }

    public override void Tick(float deltaTime)
    {
        stateElapsedTime += deltaTime;
        controller.TickAttackCooldown(deltaTime);

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

        controller.RotateTowards(controller.TargetPlayer.position, deltaTime);

        if (attackInProgress)
        {
            if (!controller.HasBaseAnimationFinished(controller.attackStateName))
            {
                return;
            }

            attackInProgress = false;
            PlayBattleIdle();
        }

        if (controller.IsPlayerLost())
        {
            controller.ChangeState(MonsterStateType.Return);
            return;
        }

        if (stateElapsedTime >= controller.MinAttackStateDuration && controller.ShouldExitAttackState())
        {
            controller.ChangeState(MonsterStateType.Chase);
            return;
        }

        if (!controller.IsPlayerInAttackRange())
        {
            return;
        }

        if (!controller.IsAttackCooldownReady())
        {
            if (!attackInProgress && !isInBattleIdle)
            {
                PlayBattleIdle();
            }
            return;
        }

        StartAttack();
    }

    private void StartAttack()
    {
        attackInProgress = true;
        isInBattleIdle = false;
        controller.StartAttackCooldown();
        controller.PlayBaseAnimation(controller.attackStateName);
    }

    private void PlayBattleIdle()
    {
        attackInProgress = false;
        isInBattleIdle = true;
        controller.PlayBaseAnimation(controller.idleBattleStateName);
    }
}
