using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Idle : State
{
    readonly PlayerContext ctx;
    public Idle(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }
    protected override State GetTransition()
    {
        return (Mathf.Abs(ctx.move.x) > 0.01f || Mathf.Abs(ctx.move.z) > 0.01f) ? ((Grounded)Parent).Move : null;
    }

    protected override void OnEnter()
    {
        //TODO: 第三人称
        ctx.moveSpeed = 0f;
    }
}
