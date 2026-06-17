using UnityEngine;

public class FlyingState : State
{
    readonly PlayerContext ctx;

    public FlyingState(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (!ctx.flyUnlocked || !ctx.jumpHeld)
        {
            return ((Airborne)Parent).Fall;
        }

        if (ctx.currentFlightEnergy <= 0f)
        {
            return ((Airborne)Parent).HoverFall;
        }

        return null;
    }

    protected override void OnEnter()
    {
        ctx.isFlying = true;
        ctx.isGliding = false;
        ctx.ySpeed = Mathf.Max(ctx.ySpeed, ctx.flyRiseSpeed);
        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.flyAnimStateName, ctx.airborneBlendDuration);
        }
    }

    protected override void OnExit()
    {
        ctx.isFlying = false;
        if (!ctx.jumpHeld || ctx.currentFlightEnergy <= 0f)
        {
            ctx.ySpeed = Mathf.Min(ctx.ySpeed, ctx.flyFallSpeed);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        Vector3 targetVelocity = ctx.worldMoveDir.sqrMagnitude > 0.0001f
            ? ctx.worldMoveDir * ctx.flyHorizontalSpeed
            : Vector3.zero;

        Vector3 horizontalVelocity = new Vector3(ctx.airVelocity.x, 0f, ctx.airVelocity.z);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, ctx.flyAcceleration * deltaTime);
        ctx.airVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        ctx.ySpeed = ctx.flyRiseSpeed;
        ctx.currentFlightEnergy = Mathf.Max(0f, ctx.currentFlightEnergy - ctx.flightEnergyConsumeRate * deltaTime);
    }
}
