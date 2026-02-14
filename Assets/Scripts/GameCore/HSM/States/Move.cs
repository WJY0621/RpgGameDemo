using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : State
{
    readonly PlayerContext ctx;
    public readonly Run Run;
    public readonly Walking walking;
    public Move(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
        Run = new Run(machine, this, ctx);
        walking = new Walking(machine, this, ctx);
    }

    protected override State GetInitialState() => walking;
    
    protected override State GetTransition()
    {
        if (!ctx.grounded) return ((PlayerRoot)Parent).Airborne;

        return (Mathf.Abs(ctx.move.x) <= 0.01f && Mathf.Abs(ctx.move.z) <= 0.01)? ((Grounded)Parent).Idle : null;
    }

    protected override void OnEnter()
    {
        ctx.isMoving = true;
    }

    protected override void OnExit()
    {
        ctx.isMoving = false;
    }
}
