using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRoot : State
{
    public readonly Grounded Grounded;
    public readonly Airborne Airborne;
    public readonly Atk Atk;
    readonly PlayerContext ctx;

    public PlayerRoot(StateMachine m, PlayerContext ctx) : base(m, null)
    {
        this.ctx = ctx;
        Grounded = new Grounded(m, this, ctx);
        Airborne = new Airborne(m, this, ctx);
        Atk = new Atk(m, this, ctx);
    }

    protected override State GetInitialState() => Grounded;//默认状态为地面状态
    //protected override State GetTransition() => ctx.grounded ? null : Airborne;//判断是否脱离地面
}
