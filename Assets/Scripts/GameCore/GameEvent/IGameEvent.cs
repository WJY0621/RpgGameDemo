using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 自定义事件接口
/// </summary>
public interface IGameEvent
{
    //得到事件
    public GameActionBase GetAction();
    //设置事件
    public void SetAction(GameActionBase gameAction);
    //添加事件
    public void AddAction(GameActionBase gameAction);
    //删除事件
    public void SubAction(GameActionBase gameAction);
    //调用事件
    public void Invoke(IGameEventParameter parameter);
}
