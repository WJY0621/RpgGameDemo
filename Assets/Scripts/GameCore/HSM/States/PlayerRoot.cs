using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRoot : State
{
    private const string AttackLayerName = "UpperBody";
    private const string AttackLayerStateName = "Atk";
    private const string AttackLayerDefaultStateName = "Empty";

    public readonly Grounded Grounded;
    public readonly Airborne Airborne;
    public readonly LandingState Landing;
    readonly PlayerContext ctx;

    public PlayerRoot(StateMachine m, PlayerContext ctx) : base(m, null)
    {
        this.ctx = ctx;
        Grounded = new Grounded(m, this, ctx);
        Airborne = new Airborne(m, this, ctx);
        Landing = new LandingState(m, this, ctx);
    }

    protected override State GetInitialState() => Grounded;

    protected override void OnEnter()
    {
        ctx.attackLayerActive = false;
        ResetAttackLayer();
    }

    protected override void OnUpdate(float deltaTime)
    {
        UpdateAttackLayer();
    }

    private void UpdateAttackLayer()
    {
        if (!PlayerStateDriver.HasPlayableAnimator(ctx.anim) || !ctx.useLegacyAttackLayer)
        {
            if (ctx.attackLayerActive)
            {
                ctx.attackLayerActive = false;
                ResetAttackLayer();
            }

            return;
        }

        if (ctx.isLeftPressed)
        {
            if (!ctx.attackLayerActive)
            {
                int atkLayerIndex = ctx.anim.GetLayerIndex(AttackLayerName);
                if (atkLayerIndex >= 0)
                {
                    ctx.attackLayerActive = true;
                    ctx.anim.SetLayerWeight(atkLayerIndex, 1f);
                    ctx.anim.Play(AttackLayerStateName, atkLayerIndex, 0f);
                }
            }

            return;
        }

        if (!ctx.attackLayerActive)
        {
            return;
        }

        ctx.attackLayerActive = false;
        ResetAttackLayer();
    }

    private void ResetAttackLayer()
    {
        if (!PlayerStateDriver.HasPlayableAnimator(ctx.anim))
        {
            return;
        }

        int atkLayerIndex = ctx.anim.GetLayerIndex(AttackLayerName);
        if (atkLayerIndex >= 0)
        {
            ctx.anim.Play(AttackLayerDefaultStateName, atkLayerIndex, 0f);
            ctx.anim.SetLayerWeight(atkLayerIndex, 0f);
        }
    }
}
