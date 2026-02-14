using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfoPanel : BasePanel
{
    private Transform UIItemName;
    private Transform UIItemInfo;
    private Transform UIItemDescription;
    private Transform UIUseButton;
    private Transform UIDiscardButton;

    public override void Init()
    {
        InitUIName();
        InitClick();
    }

    public void UpdatePanelInfo(Item item)
    {
        // 先确保初始化完成
        if (UIItemName == null) InitUIName();

        // 统一处理 TextMeshProUGUI 和 Text
        SetText(UIItemName, item.name);
        SetText(UIItemDescription, item.description);
        SetText(UIItemInfo, item.functionDescription);
    }

    private void SetText(Transform trans, string content)
    {
        if (trans == null) return;
        var tmp = trans.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = content;
            return;
        }
        var txt = trans.GetComponent<Text>();
        if (txt != null)
        {
            txt.text = content;
        }
    }

    private void InitUIName()
    {
        UIItemName = transform.Find("ItemName");
        UIItemInfo = transform.Find("SkillInfo");
        UIItemDescription = transform.Find("ItemInfo");
        UIUseButton = transform.Find("UseButton");
        UIDiscardButton = transform.Find("DiscardButton");
    }
    private void InitClick()
    {
        if(UIUseButton) UIUseButton.GetComponent<Button>().onClick.AddListener(OnUseButtonClick);
        if(UIDiscardButton) UIDiscardButton.GetComponent<Button>().onClick.AddListener(OnDiscardButtonClick);
    }

    private void OnDiscardButtonClick()
    {
        
    }

    private void OnUseButtonClick()
    {
        
    }
}
