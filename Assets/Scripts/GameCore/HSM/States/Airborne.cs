using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Airborne : State
{
    readonly PlayerContext ctx;
    public Airborne(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (ctx.isLeftPressed)
        {
            return ((PlayerRoot)Parent).Atk;
        }
        return ctx.grounded ? ((PlayerRoot)Parent).Grounded : null;
    }

    protected override void OnEnter()
    {
        //TODO: 通过ctx.anim 更新动画
        if(ctx.ySpeed > 0)
        {
            ctx.anim.CrossFade("Jump", 0.5f);
        }
        else
        {
            ctx.anim.CrossFade("Fall", 0.5f);
        }
        if(ctx.moveSpeed == 0)
        {
            ctx.moveSpeed = 1f;
        }
    }
    protected override void OnExit()
    {
        ctx.anim.CrossFade("Land", 0.1f);
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.ySpeed += ctx.fallSpeed * deltaTime;
    }
}
