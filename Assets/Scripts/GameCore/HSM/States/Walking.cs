using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Walking : State
{
    readonly PlayerContext ctx;

    public Walking(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (GameMgr.input.Data.RunInput)
        {
            return ((Move)Parent).Run;
        }

        return null;
    }

    protected override void OnEnter()
    {
        ctx.isWalkingState = true;
        ctx.isRunningState = false;

        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.walkAnimStateName, ctx.locomotionBlendDuration);
        }
    }

    protected override void OnExit()
    {
        ctx.isWalkingState = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        float targetSpeed = ctx.walkSpeed;
        ctx.moveSpeed = Mathf.MoveTowards(ctx.moveSpeed, targetSpeed, ctx.accel * deltaTime);
    }
}
