using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInputStateData
{
    [Header("按钮设置")]
    public Vector2 dirKeyAxis = new Vector2();
    public Vector2 DirKeyAxis
    {
        get { return dirKeyAxis; }
        set
        {
            dirKeyAxis = value;
            NormalInputX = (int)(dirKeyAxis * Vector2.right).normalized.x;
            NormalInputY = (int)(dirKeyAxis * Vector2.up).normalized.y;
            //Debug.Log("输入方向成功:"+dirKeyAxis);
        }
    }
    public Vector2 Look;
    public int NormalInputX;
    public int NormalInputY;
    public bool RunInput;

    public bool JumpInput;

    public bool CroushInput;

    public bool Interction;

    public bool PushDialogue;

    public bool SwitchInventory;

    public bool SwitchPause;

    public bool Cancel;
    public bool Fire;
    public bool RightPress;

    public void ResetAllButton()
    {
        JumpInput = false;
        //CroushInput = false;
        Interction = false;
        PushDialogue = false;
        SwitchInventory = false;
        SwitchPause = false;
        Cancel = false;
        //Debug.Log("重置");
    }
}
