using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Atk : State
{
    // 这个状态不是攻击判定状态，只表示“攻击时的移动方式”。
    readonly PlayerContext ctx;
    public Atk(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        if (!ctx.grounded)
        {
            return ((PlayerRoot)Parent.Parent).Airborne;
        }

        if (!ctx.isLeftPressed)
        {
            bool hasMoveInput = Mathf.Abs(ctx.move.x) > 0.01f || Mathf.Abs(ctx.move.z) > 0.01f;
            return hasMoveInput ? ((Grounded)Parent).Move : ((Grounded)Parent).Idle;
        }

        return null;
    }

    protected override void OnEnter()
    {
        ctx.moveSpeed = 5f;
        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.attackAnimStateName, ctx.attackBlendDuration, 0);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.ySpeed = ctx.groundedStickForce;

        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            PlayerStateDriver.SetAnimatorFloatIfExists(ctx.anim, "DirX", ctx.move.x);
            PlayerStateDriver.SetAnimatorFloatIfExists(ctx.anim, "DirZ", ctx.move.z);
        }
    }
}
