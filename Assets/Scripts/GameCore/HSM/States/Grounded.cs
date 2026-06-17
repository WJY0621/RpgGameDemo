using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grounded : State
{
    readonly PlayerContext ctx;
    public readonly Idle Idle;
    public readonly Move Move;
    // Atk 是 Grounded 的子状态，语义是“攻击时的移动方式”。
    // 它不是攻击系统本身，只是地面上的一种 locomotion 分支。
    public readonly Atk Atk;

    public Grounded(StateMachine machine, State parent, PlayerContext ctx) : base(machine, parent)
    {
        this.ctx = ctx;
        Idle = new Idle(machine, this, ctx);
        Move = new Move(machine, this, ctx);
        Atk = new Atk(machine, this, ctx);
    }

    protected override State GetInitialState() => Idle;

    protected override State GetTransition()
    {
        if (ctx.jumpPressed)
        {
            PrepareJumpEntry();
            ctx.jumpPressed = false;
            return ((PlayerRoot)Parent).Airborne;
        }

        if (ctx.weaponAttackEnabled && ctx.isLeftPressed && !(Leaf() is Atk))
        {
            // 地面左键：进入攻击时的移动方式状态。
            return Atk;
        }

        if (!ctx.grounded)
        {
            PrepareFallEntry();
            return ((PlayerRoot)Parent).Airborne;
        }

        return null;
    }
    protected override void OnEnter()
    {
        ctx.ySpeed = ctx.groundedStickForce;
        ctx.airVelocity = Vector3.zero;
        ctx.landingCarryVelocity = Vector3.zero;
        ctx.remainingAirJumps = Mathf.Max(0, ctx.extraJumpCount);
        ctx.isFlying = false;
        ctx.isGliding = false;
        if (ctx.restoreFlightEnergyImmediatelyOnGround)
        {
            ctx.currentFlightEnergy = Mathf.Max(0f, ctx.maxFlightEnergy);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.ySpeed = ctx.groundedStickForce;
        if (!ctx.restoreFlightEnergyImmediatelyOnGround)
        {
            ctx.currentFlightEnergy = Mathf.MoveTowards(
                ctx.currentFlightEnergy,
                Mathf.Max(0f, ctx.maxFlightEnergy),
                Mathf.Max(0f, ctx.flightEnergyRecoverRate) * deltaTime);
        }
    }

    private void PrepareJumpEntry()
    {
        ctx.ySpeed = ctx.jumpSpeed;
        ctx.airVelocity = ctx.worldMoveDir * GetAirborneHorizontalSpeed();
    }

    private void PrepareFallEntry()
    {
        ctx.airVelocity = ctx.worldMoveDir * GetAirborneHorizontalSpeed();
    }
    private float GetAirborneHorizontalSpeed()
    {
        State activeLeaf = Leaf();
        if (activeLeaf is Run)
        {
            return ctx.runJumpHorizontalSpeed;
        }

        if (activeLeaf is Walking)
        {
            return ctx.walkJumpHorizontalSpeed;
        }

        return ctx.idleJumpHorizontalSpeed;
    }
}
