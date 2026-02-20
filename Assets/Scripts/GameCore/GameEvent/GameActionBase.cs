using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//委托基类 用于封装事件的委托
public class GameActionBase
{
    
}
/// <summary>
/// 无参数委托
/// </summary>
public class GameActionNoneParam : GameActionBase
{
    //提供一个无参数的回调委托 给外部
    public Action noneParamCallBack;
    public GameActionNoneParam(Action noneParamCallBack)
    {
        this.noneParamCallBack = noneParamCallBack;
    }
}

public class GameActionOneParam<T1> : GameActionBase
{
    public Action<T1> oneParamCallBack;
    public GameActionOneParam(Action<T1> oneParamCallBack)
    {
        this.oneParamCallBack = oneParamCallBack;
    }
}
public class GameActionTwoParam<T1, T2> : GameActionBase
{
    public Action<T1, T2> twoParamCallBack;
    public GameActionTwoParam(Action<T1, T2> twoParamCallBack)
    {
        this.twoParamCallBack = twoParamCallBack;
    }
}

public class GameActionThreeParam<T1, T2, T3> : GameActionBase
{
    public Action<T1, T2, T3> threeParamCallBack;
    public GameActionThreeParam(Action<T1, T2, T3> threeParamCallBack)
    {
        this.threeParamCallBack = threeParamCallBack;
    }
}