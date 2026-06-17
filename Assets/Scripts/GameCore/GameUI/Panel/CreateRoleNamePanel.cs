using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class CreateRoleNamePanel : BasePanel
{
    private Transform UIEnsureButton;
    private Transform UICancelButton;
    private Transform UIRoleNameInput;

    // 防止重复点击
    private bool isCreating = false;

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
        UIEnsureButton = transform.Find("PanelBk/EnsureButton");
        UICancelButton = transform.Find("PanelBk/CancelButton");
        UIRoleNameInput = transform.Find("NameField");
    }

    private void InitClick()
    {
        if (UIEnsureButton != null)
            UIEnsureButton.GetComponent<Button>().onClick.AddListener(OnClickEnsure);
        if (UICancelButton != null)
            UICancelButton.GetComponent<Button>().onClick.AddListener(OnClickCancel);
    }

    private async void OnClickEnsure()
    {
        // 防止重复点击
        if (isCreating) return;

        // 获取输入的玩家姓名
        TMP_InputField inputField = UIRoleNameInput?.GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            return;
        }

        string playerName = inputField?.text;

        if (string.IsNullOrEmpty(playerName))
        {
            return;
        }

        isCreating = true;

        // 创建新存档，传入玩家姓名和角色模型名称
        GameMgr.File.CreateNewGame(playerName, CreateRolePanel.currentSelectedRoleModelName);
        GameMgr.Account?.SetDisplayName(playerName);

        // 设置当前角色模型名称，以便进入游戏后替换模型
        PlayerModelManager.CurrentRoleModelName = CreateRolePanel.currentSelectedRoleModelName;

        LoadingResult result = await GameMgr.Scene.LoadSceneAsync("GameScene");
        if (!result.Success)
        {
            isCreating = false;
            Debug.LogError($"[CreateRoleNamePanel] Failed to enter game scene: {result.ErrorMessage}");
        }
    }

    private void OnClickCancel()
    {
        // 取消创建，返回上层
        this.Hide();
    }
}
