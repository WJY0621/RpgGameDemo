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

    private void OnClickEnsure()
    {
        Debug.Log("[CreateRoleNamePanel] OnClickEnsure called");

        // 获取输入的玩家姓名
        TMP_InputField inputField = UIRoleNameInput?.GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            Debug.LogError("[CreateRoleNamePanel] InputField component not found!");
            return;
        }

        string playerName = inputField?.text;
        Debug.Log($"[CreateRoleNamePanel] Input playerName: '{playerName}'");

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.Log("[CreateRoleNamePanel] 请输入玩家姓名");
            return;
        }

        Debug.Log($"[CreateRoleNamePanel] 创建角色，姓名: {playerName}");

        // 创建新存档，传入玩家姓名
        GameMgr.File.CreateNewGame(playerName);

        Debug.Log("[CreateRoleNamePanel] Save created, destroying panels and loading scene...");

        // 直接销毁所有UI面板，进入游戏场景
        GameMgr.UI.DestroyAllPanels();
        GameMgr.Scene.LoadSceneAsync("GameScene").Forget();
    }

    private void OnClickCancel()
    {
        // 取消创建，返回上层
        this.Hide();
    }
}
