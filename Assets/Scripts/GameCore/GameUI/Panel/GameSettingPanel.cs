using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameSettingPanel : BasePanel
{
    private Transform UICloseButton;
    private Transform UISaveButton;
    private Transform UIExitButton;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }
    public override void Init()
    {
        InitUI();
        InitClick();
    }

    private void InitUI()
    {
        UICloseButton = transform.Find("PanelBK/CloseButton");
        UISaveButton = transform.Find("SaveButton");
        UIExitButton = transform.Find("ExitButton");
    }

    private void InitClick()
    {
        UICloseButton.GetComponent<Button>().onClick.AddListener(OnCloseClick);
        UISaveButton.GetComponent<Button>().onClick.AddListener(OnSaveClick);
        UIExitButton.GetComponent<Button>().onClick.AddListener(OnExitClick);
    }

    private void OnCloseClick()
    {
        this.Hide();
    }
    private void OnSaveClick()
    {
        GameMgr.File.SaveGameFile();
        Debug.Log("存档成功");
    }
    private async void OnExitClick()
    {
        TipPanel tipPanel = await GameMgr.UI.ShowPanel<TipPanel>();
        await tipPanel.ShowTip("是否退出游戏？", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }, () =>
        {
            // 点击取消或背景，什么都不做
        });
    }
}
