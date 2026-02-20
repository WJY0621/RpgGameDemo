using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorMgr
{
    private bool isCursorVisible = false;

    public void Init()
    {
        // 默认初始化为显示状态（UI模式，适用于开始菜单）
        SetCursorState(true);
    }

    /// <summary>
    /// 设置鼠标状态
    /// </summary>
    /// <param name="visible">true: 显示并解锁; false: 隐藏并锁定</param>
    public void SetCursorState(bool visible)
    {
        isCursorVisible = visible;
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    /// <summary>
    /// 切换鼠标状态
    /// </summary>
    public void ToggleCursorState()
    {
        SetCursorState(!isCursorVisible);
    }

    public bool IsCursorVisible()
    {
        return isCursorVisible;
    }
}
