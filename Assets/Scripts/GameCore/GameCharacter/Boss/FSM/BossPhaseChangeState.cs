public class BossPhaseChangeState : BossState
{
    private float elapsedTime;
    private BossPhaseConfig phase;

    public BossPhaseChangeState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.PhaseChange;

    public override void Enter()
    {
        elapsedTime = 0f;
        phase = controller.PendingPhase;
        controller.StopMovement();

        if (phase != null && phase.invincibleDuringTransition)
        {
            controller.Health?.SetInvincible(true);
        }

        if (phase != null && !string.IsNullOrWhiteSpace(phase.phaseEnterAnimation))
        {
            controller.PlayBaseAnimation(phase.phaseEnterAnimation);
        }
        else
        {
            controller.PlayBaseAnimation(controller.IdleBattleStateName);
        }
    }

    public override void Exit()
    {
        controller.Health?.SetInvincible(false);
        controller.ClearPendingPhase();
    }

    public override void Tick(float deltaTime)
    {
        elapsedTime += deltaTime;

        if (controller.IsDead())
        {
            controller.ChangeState(BossStateType.Dead);
            return;
        }

        float minDuration = phase != null ? phase.phaseEnterMinDuration : 0f;
        if (elapsedTime < minDuration)
        {
            return;
        }

        if (phase != null && !string.IsNullOrWhiteSpace(phase.phaseEnterAnimation)
            && !controller.HasBaseAnimationFinished(phase.phaseEnterAnimation, 0.95f))
        {
            return;
        }

        controller.ChangeState(controller.HasValidTarget() ? BossStateType.Chase : BossStateType.Idle);
    }
}
