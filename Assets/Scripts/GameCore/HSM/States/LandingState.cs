using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LandingState : State
{
    readonly PlayerContext ctx;

    public LandingState(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition()
    {
        return IsAnimationFinished(ctx.landAnimStateName) ? ((PlayerRoot)Parent).Grounded : null;
    }

    protected override void OnEnter()
    {
        ctx.ySpeed = 0f;
        ctx.landingCarryVelocity = new Vector3(ctx.airVelocity.x, 0f, ctx.airVelocity.z);
        ctx.airVelocity = Vector3.zero;
        PlayerAudioController audioController = ctx.cc != null ? ctx.cc.GetComponent<PlayerAudioController>() : null;
        audioController?.PlayLanding();

        if (PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            ctx.anim.CrossFade(ctx.landAnimStateName, ctx.landingBlendDuration);
        }
    }

    bool IsAnimationFinished(string stateName)
    {
        if (!PlayerStateDriver.HasPlayableAnimator(ctx.anim) || ctx.anim.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo info = ctx.anim.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName) && info.normalizedTime >= ctx.landingCompleteNormalizedTime;
    }
}
