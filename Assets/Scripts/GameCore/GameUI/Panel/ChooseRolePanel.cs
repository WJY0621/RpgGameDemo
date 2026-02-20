using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChooseRolePanel : BasePanel
{
    private Transform UIBackButton;
    private Transform UIStartButton;
    private Transform UIRoleMod;
    private Transform UIScrollViewContent;
    private Transform UIAddRoleButton1;
    private Transform UIAddRoleButton2;
    private Transform UIRoleName;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        UIBackButton = transform.Find("BackButton");
        UIStartButton = transform.Find("StartButton");
        UIRoleMod = transform.Find("RoleMod");
        UIScrollViewContent = transform.Find("Scroll View/Viewport/Content");
        UIAddRoleButton1 = transform.Find("AddRoleButton1");
        UIAddRoleButton2 = transform.Find("AddRoleButton2");
        UIRoleName = transform.Find("RoleName");
        InitClick();
    }

    public void InitClick()
    {
        UIBackButton.GetComponent<Button>().onClick.AddListener(OnClickBack);
        UIAddRoleButton1.GetComponent<Button>().onClick.AddListener(OnClickAddRole);
        UIAddRoleButton2.GetComponent<Button>().onClick.AddListener(OnClickAddRole);
        UIStartButton.GetComponent<Button>().onClick.AddListener(OnClickStart);
    }
    private void OnClickBack()
    {
        GameMgr.UI.HidePanel<ChooseRolePanel>();
    }
    private void OnClickAddRole()
    {
        
    }
    private void OnClickStart()
    {
        //TODO 切换到游戏场景
    }

    public void SetRoleName(string name)
    {
        UIRoleName.GetComponent<Text>().text = name;
    }
}
