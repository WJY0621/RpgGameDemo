using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpState : State
{
    readonly PlayerContext ctx;

    public JumpState(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (ctx.flyUnlocked && ctx.jumpPressed)
        {
            ctx.jumpPressed = false;
            return ctx.currentFlightEnergy > 0f
                ? ((Airborne)Parent).Flying
                : ((Airborne)Parent).HoverFall;
        }

        if (ctx.jumpPressed && ctx.remainingAirJumps > 0)
        {
            ctx.remainingAirJumps--;
            ctx.ySpeed = ctx.jumpSpeed;
            ctx.jumpPressed = false;
            if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
            {
                ctx.anim.CrossFade(ctx.jumpAnimStateName, ctx.airborneBlendDuration);
            }
            return null;
        }

        return ctx.ySpeed < 0f ? ((Airborne)Parent).Fall : null;
    }

    protected override void OnEnter()
    {
        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.jumpAnimStateName, ctx.airborneBlendDuration);
        }
    }
}
