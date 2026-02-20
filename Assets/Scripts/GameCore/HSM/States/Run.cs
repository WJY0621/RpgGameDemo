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
        //if (!ctx.grounded) return ((PlayerRoot)Parent.Parent).Airborne;
        //if (Mathf.Abs(ctx.move.x) <= 0.01 && Mathf.Abs(ctx.move.z)<= 0.01f) return ((Grounded)Parent.Parent).Idle;
        if (!GameMgr.input.Data.RunInput) return ((Move)Parent).walking;
        return null;
    }

    protected override void OnUpdate(float deltaTime)
    {
        //地面移动核心逻辑
        // var targetX = ctx.move.x * ctx.runSpeed;
        // var targetZ = ctx.move.z * ctx.runSpeed;
        // ctx.velocity.x = Mathf.MoveTowards(ctx.velocity.x, targetX, ctx.accel * deltaTime);
        // ctx.velocity.z = Mathf.MoveTowards(ctx.velocity.z, targetZ, ctx.accel * deltaTime);
        var targetSpeed = ctx.runSpeed;
        ctx.moveSpeed = Mathf.MoveTowards(ctx.moveSpeed, targetSpeed, ctx.accel * deltaTime);         
    }
}
