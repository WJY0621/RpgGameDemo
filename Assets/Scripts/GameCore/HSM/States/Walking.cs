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
        //if (!ctx.grounded) return ((PlayerRoot)Parent.Parent).Airborne;
        //if (Mathf.Abs(ctx.move.x) <= 0.01 && Mathf.Abs(ctx.move.z)<= 0.01f) return ((Grounded)Parent.Parent).Idle;
        if (GameMgr.input.Data.RunInput) return ((Move)Parent).Run;
        return null;
    }

    protected override void OnUpdate(float deltaTime)
    {
        //地面移动核心逻辑
        // var targetX = ctx.move.x * ctx.moveSpeed;
        // var targetZ = ctx.move.z * ctx.moveSpeed;
        // ctx.velocity.x = Mathf.MoveTowards(ctx.velocity.x, targetX, ctx.accel * deltaTime);
        // ctx.velocity.z = Mathf.MoveTowards(ctx.velocity.z, targetZ, ctx.accel * deltaTime); 
        var targetSpeed = ctx.walkSpeed;
        ctx.moveSpeed = Mathf.MoveTowards(ctx.moveSpeed, targetSpeed, ctx.accel * deltaTime);        
    }
}
