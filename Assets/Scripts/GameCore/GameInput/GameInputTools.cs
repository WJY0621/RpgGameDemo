using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ActionType
{
    //开始
    Started,
    //执行
    Performed,
    //取消
    Canceled,
    //除开始以外
    ExceptStarted,
    ExceptPerformed,
    ExceptCanceled,
    ALL
}

public static class GameInputTools
{
    /// <summary>
    /// 为生成的playerInputControl类添加注册回调函数的方法
    /// </summary>
    /// <param name="inputs">添加方法的类本体</param>
    /// <param name="action"></param>
    /// <param name="actionType"></param>
    /// <param name="callback"></param>
    public static void RegisterActionCallBack(this PlayerInputControl inputs, InputAction action, ActionType actionType,
            Action<InputAction.CallbackContext> callback)
    {
        switch (actionType)
        {
            case ActionType.Started:
                action.started += callback;
                break;
            case ActionType.Performed:
                action.performed += callback;
                break;
            case ActionType.Canceled:
                action.canceled += callback;
                break;
            case ActionType.ExceptStarted:
                action.performed += callback;
                action.canceled += callback;
                break;
            case ActionType.ExceptPerformed:
                action.started += callback;
                action.canceled += callback;
                break;
            case ActionType.ExceptCanceled:
                action.started += callback;
                action.performed += callback;
                break;
            case ActionType.ALL:
                action.started += callback;
                action.performed += callback;
                action.canceled += callback;
                break;
        }
    }

    public static void UnRegisterActionCallBack(this PlayerInput inputs, InputAction action, ActionType actionType,
        Action<InputAction.CallbackContext> callback)
    {
        action.started -= callback;
        action.performed -= callback;
        action.canceled -= callback;
    }
}
