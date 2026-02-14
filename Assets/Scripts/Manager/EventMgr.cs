using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventMgr
{
    //存储事件
    public Dictionary<string, IGameEvent> eventsDic = new Dictionary<string, IGameEvent>();

    /// <summary>
    /// 注册事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="gameEvent">传入的自定义事件</param>
    public void Register(string eventName, IGameEvent gameEvent)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        if (eventsDic.ContainsKey(eventName))
        {
            eventsDic[eventName].AddAction(gameEvent.GetAction());
        }
        else
        {
            eventsDic.Add(eventName, gameEvent);
            eventsDic[eventName].SetAction(gameEvent.GetAction());
        }
    }
    /// <summary>
    /// 注销事件
    /// </summary>
    /// <param name="eventName">需要注销的事件名称</param>
    /// <param name="gameEvent"></param>
    public void Unregister(string eventName, IGameEvent gameEvent)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        if (eventsDic.ContainsKey(eventName))
        {
            eventsDic[eventName].SubAction(gameEvent.GetAction());
        }
        else
        {
            Debug.Log("没有该事件，无法注销");
        }
    }

    /// <summary>
    /// 触发事件
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="parameter">事件的参数</param>
    public void Broadcast(string eventName, IGameEventParameter parameter)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        if (eventsDic.ContainsKey(eventName))
        {
            eventsDic[eventName].Invoke(parameter);
        }
        else
        {
            Debug.Log("触发了一个没有的事件");
        }
    }
}
