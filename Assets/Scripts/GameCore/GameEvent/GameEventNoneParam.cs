using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEventNoneParam : IGameEvent
{
    //无参数委托作为属性
    public GameActionNoneParam gameAction;

    /// <summary>
    /// 外部传入无参数委托
    /// </summary>
    /// <param name="gameAction">传入一个无参数委托</param>
    public GameEventNoneParam(GameActionBase gameAction)
    {
        this.gameAction = (GameActionNoneParam)gameAction;
    }
    //得到事件
    public GameActionBase GetAction()
    {
        return gameAction;
    }
    //设置事件
    public void SetAction(GameActionBase gameAction)
    {
        this.gameAction.noneParamCallBack = (gameAction as GameActionNoneParam).noneParamCallBack;
    }
    //添加事件
    public void AddAction(GameActionBase gameAction)
    {
        this.gameAction.noneParamCallBack += (gameAction as GameActionNoneParam).noneParamCallBack;
    }
    //删除事件
    public void SubAction(GameActionBase gameAction)
    {
        this.gameAction.noneParamCallBack += (gameAction as GameActionNoneParam).noneParamCallBack;
    }
    //调用事件
    public void Invoke(IGameEventParameter parameter)
    {
        this.gameAction.noneParamCallBack?.Invoke();
    }
}

public class GameEventOneParam<T1> : IGameEvent
{
    //一个参数委托作为属性
    public GameActionOneParam<T1> gameAction;
    /// <summary>
    /// 一个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionOneParam</param>
    public GameEventOneParam(GameActionBase gameAction)
    {
        this.gameAction = (GameActionOneParam<T1>)gameAction;
    }
    /// <summary>
    /// 得到对应的事件委托类
    /// </summary>
    /// <returns></returns>
    public GameActionBase GetAction()
    {
        return this.gameAction;
    }
    /// <summary>
    /// 一个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionOneParam</param>
    public void SetAction(GameActionBase gameAction)
    {
        this.gameAction.oneParamCallBack = (gameAction as GameActionOneParam<T1>).oneParamCallBack;
    }
    /// <summary>
    /// 一个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionOneParam</param>
    public void AddAction(GameActionBase gameAction)
    {
        this.gameAction.oneParamCallBack += (gameAction as GameActionOneParam<T1>).oneParamCallBack;
    }
    /// <summary>
    /// 一个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionOneParam</param>
    public void SubAction(GameActionBase gameAction)
    {
        this.gameAction.oneParamCallBack -= (gameAction as GameActionOneParam<T1>).oneParamCallBack;
    }

    public void Invoke(IGameEventParameter parameter)
    {
        if (parameter == null) return;
        GameEventParameter<T1> param = parameter as GameEventParameter<T1>;
        this.gameAction.oneParamCallBack?.Invoke(param.param1);
    }
}

public class GameEventTwoParam<T1, T2> : IGameEvent
{
    public GameActionTwoParam<T1, T2> gameAction;

    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionTwoParam</param>
    public GameEventTwoParam(GameActionBase gameAction)
    {
        this.gameAction=(GameActionTwoParam<T1, T2>)gameAction;
    }

    public GameActionBase GetAction()
    {
        return this.gameAction;
    }
    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionTwoParam</param>
    public void SetAction( GameActionBase gameAction)
    {
        this.gameAction.twoParamCallBack=(gameAction as GameActionTwoParam<T1, T2>).twoParamCallBack;
    }
    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionTwoParam</param>
    public void AddAction(GameActionBase gameAction)
    {
        this.gameAction.twoParamCallBack += (gameAction as GameActionTwoParam<T1, T2>).twoParamCallBack;
    }

    public void SubAction(GameActionBase gameAction)
    {
        this.gameAction.twoParamCallBack -= (gameAction as GameActionTwoParam<T1, T2>).twoParamCallBack;
    }

    public void Invoke(IGameEventParameter parameter)
    {
        if (parameter == null) return;
        GameEventParameter<T1, T2> param=parameter as GameEventParameter<T1, T2>;
        this.gameAction.twoParamCallBack?.Invoke(param.param1,param.param2);
    }
}

public class GameEventThreeParam<T1, T2, T3> : IGameEvent
{
    public GameActionThreeParam<T1, T2, T3> gameAction;

    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionThreearam</param>
    public GameEventThreeParam(GameActionBase gameAction)
    {
        this.gameAction = (GameActionThreeParam<T1, T2, T3>) gameAction;
    }

    public GameActionBase GetAction()
    {
        return this.gameAction;
    }
    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionTwoParam</param>
    public void SetAction( GameActionBase gameAction)
    {
        this.gameAction.threeParamCallBack = (gameAction as GameActionThreeParam<T1, T2, T3>).threeParamCallBack;
    }
    /// <summary>
    /// 二个参数事件
    /// </summary>
    /// <param name="gameAction">请传入GameActionTwoParam</param>
    public void AddAction(GameActionBase gameAction)
    {
        this.gameAction.threeParamCallBack += (gameAction as GameActionThreeParam<T1, T2, T3>).threeParamCallBack;
    }

    public void SubAction(GameActionBase gameAction)
    {
        this.gameAction.threeParamCallBack -= (gameAction as GameActionThreeParam<T1, T2, T3>).threeParamCallBack;
    }

    public void Invoke(IGameEventParameter parameter)
    {
        if (parameter == null) return;
        GameEventParameter<T1, T2, T3> param=parameter as GameEventParameter<T1, T2, T3>;
        this.gameAction.threeParamCallBack?.Invoke(param.param1, param.param2, param.param3);
    }
}
