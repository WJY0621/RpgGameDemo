using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Airborne : State
{
    readonly PlayerContext ctx;
    public readonly JumpState Jump;
    public readonly FallState Fall;
    public readonly FlyingState Flying;
    public readonly HoverFallState HoverFall;

    public Airborne(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
        Jump = new JumpState(machine, this, ctx);
        Fall = new FallState(machine, this, ctx);
        Flying = new FlyingState(machine, this, ctx);
        HoverFall = new HoverFallState(machine, this, ctx);
    }

    protected override State GetInitialState() => ctx.ySpeed < 0f ? (State)Fall : Jump;

    protected override State GetTransition()
    {
        if (ctx.grounded)
        {
            float fallSpeed = Mathf.Max(0f, -ctx.ySpeed);

            if (fallSpeed >= ctx.hardLandingFallSpeed)
            {
                return ((PlayerRoot)Parent).Landing;
            }

            return ((PlayerRoot)Parent).Grounded;
        }

        return null;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (ctx.isFlying || ctx.isGliding)
        {
            return;
        }

        ApplyAirControl(deltaTime);
        ctx.ySpeed += ctx.fallSpeed * deltaTime;
    }

    private void ApplyAirControl(float deltaTime)
    {
        Vector3 horizontalVelocity = new Vector3(ctx.airVelocity.x, 0f, ctx.airVelocity.z);
        if (ctx.worldMoveDir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float currentSpeed = horizontalVelocity.magnitude;
        float targetSpeed = Mathf.Max(currentSpeed, ctx.airControlSpeed);
        Vector3 targetVelocity = ctx.worldMoveDir * targetSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, ctx.airControlAcceleration * deltaTime);
        ctx.airVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
    }
}
