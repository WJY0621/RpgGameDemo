using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grounded : State
{
    readonly PlayerContext ctx;
    public readonly Idle Idle;
    public readonly Move Move;
    public Grounded(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
        Idle = new Idle(machine, this, ctx);
        Move = new Move(machine, this, ctx);
        //Add(new DelayActivationActivity { seconds = 0.5f });
    }

    protected override State GetInitialState() => Move;

    protected override State GetTransition()
    {
        if (ctx.jumpPressed)
        {
            ctx.ySpeed = ctx.jumpSpeed;
            ctx.jumpPressed = false;
            return ((PlayerRoot)Parent).Airborne;
        }
        if (ctx.isLeftPressed)
        {
            return ((PlayerRoot)Parent).Atk;
        }
        return ctx.grounded ? null : ((PlayerRoot)Parent).Airborne;
    }
    protected override void OnEnter()
    {
        ctx.ySpeed = 0;
    }
    protected override void OnUpdate(float deltaTime)
    {
        ctx.anim.SetFloat("Speed", ctx.moveSpeed);
    }
}
