using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Atk : State
{
    readonly PlayerContext ctx;
    public Atk(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (!ctx.isLeftPressed)
        {
            return ctx.grounded? ((PlayerRoot)Parent).Grounded : ((PlayerRoot)Parent).Airborne;
        }
        return null;
    }
    protected override void OnEnter()
    {
        ctx.anim.SetBool("ATK", true);
        ctx.moveSpeed = 2f;
        ctx.anim.SetLayerWeight(1, 1);
    }
    protected override void OnExit()
    {
        ctx.anim.SetBool("ATK", false);
        ctx.anim.SetLayerWeight(1, 0);
    }
    protected override void OnUpdate(float deltaTime)
    {
        ctx.anim.SetFloat("DirX", ctx.move.x);
        ctx.anim.SetFloat("DirZ", ctx.move.z);
    }
}
