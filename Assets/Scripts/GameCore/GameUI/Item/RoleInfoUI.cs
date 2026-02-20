using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoleInfoUI : MonoBehaviour
{
    private Transform UIRoleIcon;
    private Transform UINameText;
    private Transform UIHpText;
    private Transform UIGoldText;
    private Transform UITimeText;

    private void Awake()
    {
        InitUI();
    }

    private void InitUI()
    {
        UIRoleIcon = transform.Find("RoleIcon");
        UINameText = transform.Find("NameText");
        UIHpText = transform.Find("HpText");
        UIGoldText = transform.Find("GoldText");
        UITimeText = transform.Find("TimeText");
    }
}
