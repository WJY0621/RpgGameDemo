public class BossHurtState : BossState
{
    private float elapsedTime;

    public BossHurtState(BossController controller) : base(controller)
    {
    }

    public override BossStateType StateType => BossStateType.Hurt;

    public override void Enter()
    {
        elapsedTime = 0f;
        controller.StopMovement();
        controller.PlayBaseAnimation(controller.HurtStateName);
    }

    public override void Tick(float deltaTime)
    {
        elapsedTime += deltaTime;

        if (controller.IsDead())
        {
            controller.ChangeState(BossStateType.Dead);
            return;
        }

        if (controller.HasValidTarget())
        {
            controller.RotateTowards(controller.Target.position, deltaTime);
        }

        if (!controller.HasBaseAnimationFinished(controller.HurtStateName, controller.HurtFinishedNormalizedTime) && elapsedTime < 0.75f)
        {
            return;
        }

        controller.ChangeState(controller.HasValidTarget() ? BossStateType.Chase : BossStateType.Idle);
    }
}
