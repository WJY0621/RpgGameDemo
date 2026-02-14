using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public enum ActivityMode
{
    Inactive,
    Activating,
    Active,
    Deactivating
}

public interface IActivity
{
    //活动系统可以在整个状态转换的路径上协调异步操作 支持流程控制 延迟执行 和取消机制
    ActivityMode Mode { get; }
    /// <summary>
    /// 进入状态触发异步初始化的方法
    /// </summary>
    /// <param name="ct">取消令牌 用于取消异步</param>
    /// <returns></returns>
    Task ActivateAsync(CancellationToken ct);
    //退出状态 执行资源清理方法
    Task DeactivateAsync(CancellationToken ct);
}

//提供管理激活和停用的通用模板
public abstract class Activity : IActivity
{
    public ActivityMode Mode { get; protected set; } = ActivityMode.Inactive;//初始状态为未激活

    public virtual async Task ActivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Inactive) return;
        Mode = ActivityMode.Activating; //切换为激活中

        await Task.CompletedTask;
        Mode = ActivityMode.Active; //已激活
    }

    public virtual async Task DeactivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Active) return; //只有在激活状态时才执行

        Mode = ActivityMode.Deactivating; //标记状态为停用中
        await Task.CompletedTask;
        Mode = ActivityMode.Inactive;
    }
}


