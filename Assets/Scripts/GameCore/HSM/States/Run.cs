using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Run : State
{
    readonly PlayerContext ctx;

    public Run(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (!GameMgr.input.Data.RunInput)
        {
            return ((Move)Parent).walking;
        }

        return null;
    }

    protected override void OnEnter()
    {
        ctx.isRunningState = true;
        ctx.isWalkingState = false;

        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.runAnimStateName, ctx.locomotionBlendDuration);
        }
    }

    protected override void OnExit()
    {
        ctx.isRunningState = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        float targetSpeed = ctx.runSpeed;
        ctx.moveSpeed = Mathf.MoveTowards(ctx.moveSpeed, targetSpeed, ctx.accel * deltaTime);
    }
}
