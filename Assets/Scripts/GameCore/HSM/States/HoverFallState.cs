using UnityEngine;

public class HoverFallState : State
{
    readonly PlayerContext ctx;

    public HoverFallState(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (!ctx.flyUnlocked || !ctx.jumpHeld)
        {
            return ((Airborne)Parent).Fall;
        }

        if (ctx.currentFlightEnergy > 0f)
        {
            return ((Airborne)Parent).Flying;
        }

        return null;
    }

    protected override void OnEnter()
    {
        ctx.isFlying = false;
        ctx.isGliding = true;
        ctx.ySpeed = Mathf.Min(ctx.ySpeed, ctx.glideFallSpeed);
        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.flyAnimStateName, ctx.airborneBlendDuration);
        }
    }

    protected override void OnExit()
    {
        ctx.isGliding = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        Vector3 targetVelocity = ctx.worldMoveDir.sqrMagnitude > 0.0001f
            ? ctx.worldMoveDir * ctx.glideHorizontalSpeed
            : Vector3.zero;

        Vector3 horizontalVelocity = new Vector3(ctx.airVelocity.x, 0f, ctx.airVelocity.z);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, ctx.glideAcceleration * deltaTime);
        ctx.airVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        ctx.ySpeed = ctx.glideFallSpeed;
    }
}
