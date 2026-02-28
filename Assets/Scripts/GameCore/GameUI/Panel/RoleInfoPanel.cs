using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoleInfoPanel : MonoBehaviour
{
    private Transform UITimeText;
    private Transform UINameText;
    private Transform UIHpText;
    private Transform UIGoldText;
    private Transform UIDeleteButton;
    private Transform UISelectIcon;

    // 当前面板对应的存档数据
    private GameFile gameFile;
    // 是否被选中
    private bool isSelected;

    // 选中状态改变事件
    public System.Action<RoleInfoPanel> onSelectedChanged;

    void Awake()
    {
        InitUIName();
        InitClick();
    }

    private void InitUIName()
    {
        UITimeText = transform.Find("TimeText");
        UINameText = transform.Find("NameText");
        UIHpText = transform.Find("HpText");
        UIGoldText = transform.Find("GoldText");
        UIDeleteButton = transform.Find("DeleteButton");
        UISelectIcon = transform.Find("SelectIcon");

        // 初始时隐藏选中图标
        SetSelectIconVisible(false);

        // 添加整个面板的点击事件
        Button panelButton = GetComponent<Button>();
        if (panelButton == null)
            panelButton = gameObject.AddComponent<Button>();

        panelButton.onClick.AddListener(OnPanelClick);
    }

    private void InitClick()
    {
        if (UIDeleteButton != null)
            UIDeleteButton.GetComponent<Button>().onClick.AddListener(OnDeleteButtonClick);
    }

    public void SetData(GameFile gameFile, string roleName, string createTime)
    {
        this.gameFile = gameFile;

        // 使用 TMP_Text 组件
        if (UINameText != null)
        {
            TMP_Text textComp = UINameText.GetComponent<TMP_Text>();
            if (textComp != null)
                textComp.text = roleName;
            else
                Debug.LogWarning("[RoleInfoPanel] UINameText has no TMP_Text component!");
        }

        if (UITimeText != null)
        {
            TMP_Text textComp = UITimeText.GetComponent<TMP_Text>();
            if (textComp != null)
                textComp.text = createTime;
            else
                Debug.LogWarning("[RoleInfoPanel] UITimeText has no TMP_Text component!");
        }
    }

    // 设置选中状态
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        SetSelectIconVisible(selected);
    }

    // 获取是否选中
    public bool IsSelected => isSelected;

    // 获取关联的存档数据
    public GameFile GetGameFile() => gameFile;

    // 设置选中图标显示/隐藏
    private void SetSelectIconVisible(bool visible)
    {
        if (UISelectIcon != null)
        {
            UISelectIcon.gameObject.SetActive(visible);
        }
    }

    // 面板点击事件
    private void OnPanelClick()
    {
        // 通知 ChooseRolePanel 改变选中状态
        onSelectedChanged?.Invoke(this);
    }

    private void OnDeleteButtonClick()
    {

    }
}
