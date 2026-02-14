using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMainPanel : BasePanel
{
    private Transform UIMessagePanel;
    private Transform UIMessageText;
    private Transform UIBagButton;
    private Transform UIEquipButton;
    private Transform UIShopButton;
    private Transform UISettingButton;
    public TMP_Text MessageText;
    public CanvasGroup MessagePanel;
    protected override void Awake()
    {
        base.Awake();
        Init();
    }
    public override void Init()
    {
        InitUIName();
        InitUIEvent();
        MessageText = UIMessageText.GetComponent<TMP_Text>();
        MessagePanel = UIMessagePanel.GetComponent<CanvasGroup>();
    }
    private void InitUIName()
    {
        UIMessagePanel = transform.Find("MessagePanel");
        UIMessageText = transform.Find("MessagePanel/MessageText");
        UIBagButton = transform.Find("Button/BagButton");
        UIEquipButton = transform.Find("Button/EquipButton");
        UIShopButton = transform.Find("Button/ShopButton");
        UISettingButton = transform.Find("Button/SettingButton");
    }
    private void InitUIEvent()
    {
        UIBagButton.GetComponent<Button>().onClick.AddListener(OnClickBag);
        UIEquipButton.GetComponent<Button>().onClick.AddListener(OnClickEquip);
        UIShopButton.GetComponent<Button>().onClick.AddListener(OnClickShop);
        UISettingButton.GetComponent<Button>().onClick.AddListener(OnClickSetting);
    }

    private async void OnClickSetting()
    {
        await GameMgr.UI.ShowPanel<GameSettingPanel>();
    }

    private void OnClickShop()
    {
        
    }

    private void OnClickEquip()
    {
        
    }

    private void OnClickBag()
    {
        
    }
}
