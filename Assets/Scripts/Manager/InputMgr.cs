using UnityEngine;
using System;

public class InputMgr
{
    public PlayerInputControl playerInput;
    private GameInputStateData inputData;
    public GameInputStateData Data => inputData;

    public InputMgr()
    {
        inputData = new GameInputStateData();
        playerInput = new PlayerInputControl();

        //注册当键按下时的函数 
        /*Player Map*/
        playerInput.RegisterActionCallBack(playerInput.Player.Move, ActionType.ALL,
            (cb) => { inputData.DirKeyAxis = playerInput.Player.Move.ReadValue<Vector2>(); });
        playerInput.RegisterActionCallBack(playerInput.Player.Jump, ActionType.Started,
            (cb) => { inputData.JumpInput = true; });
        playerInput.RegisterActionCallBack(playerInput.Player.Jump, ActionType.Canceled,
            (cb) => { inputData.JumpInput = false; });
        playerInput.RegisterActionCallBack(playerInput.Player.Croush, ActionType.Started,
            (cb) => { inputData.CroushInput = true; });
        playerInput.RegisterActionCallBack(playerInput.Player.Croush, ActionType.Canceled,
            (cb) => { inputData.CroushInput = false; });
        playerInput.RegisterActionCallBack(playerInput.Player.Run, ActionType.Started,
            (cb) => { inputData.RunInput = true; });
        playerInput.RegisterActionCallBack(playerInput.Player.Run, ActionType.Canceled,
            (cb) => { inputData.RunInput = false; });
        playerInput.RegisterActionCallBack(playerInput.Player.Look, ActionType.ALL,
            (cb) => { inputData.Look = playerInput.Player.Look.ReadValue<Vector2>();});
        playerInput.RegisterActionCallBack(playerInput.Player.Fire, ActionType.Started,
            (cb) => { inputData.Fire = true;});
        playerInput.RegisterActionCallBack(playerInput.Player.Fire, ActionType.Canceled,
            (cb) => { inputData.Fire = false;});
        playerInput.RegisterActionCallBack(playerInput.Player.RightMouse, ActionType.Started,
            (cb) => { inputData.RightPress = true;});
        playerInput.RegisterActionCallBack(playerInput.Player.RightMouse, ActionType.Canceled,
            (cb) => { inputData.RightPress = false;});

        /*UI Map*/
        playerInput.RegisterActionCallBack(playerInput.UI.Click, ActionType.Performed,
            (cb) => { inputData.PushDialogue = true;});
        playerInput.RegisterActionCallBack(playerInput.UI.SpaceClick, ActionType.Performed,
            (cb) => { inputData.PushDialogue = true;});
    }

    public void ResetAllButtonValueOnLastUpdate()
    {
        this.Data.ResetAllButton();
    }
    public void EnablePlayerActionMap()
    {
        playerInput.Player.Enable();
        playerInput.UI.Disable();
    }

    public void EnableUIActionMap()
    {
        playerInput.Player.Disable();
        playerInput.UI.Enable();
    }
}
