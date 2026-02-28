using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class CreateRolePanel : BasePanel
{
    private Transform UIChooseSexButton;
    private Transform UIManButton;
    private Transform UIWomenButton;
    private Transform UIBackButton;
    private Transform UIModelPosition;
    private Transform UICompleteButton;

    private Transform UIChooseRoleButton;
    private Transform UIChooseRolePanel;

    private Transform UIRoleIconBk;
    private Transform UIRoleInfo;
    private Transform UICreateRolePanelBK;
    private Transform UIManSelectIcon;
    private Transform UIWomenSelectIcon;

    // 角色选择相关
    private GameObject roleChooseIconPrefab;
    private Transform roleIconContainer;
    private RoleListData roleListData;
    private List<RoleChooseIcon> maleRoleIcons = new List<RoleChooseIcon>();
    private List<RoleChooseIcon> femaleRoleIcons = new List<RoleChooseIcon>();
    private bool isMaleSelected = false; // 默认选中女性

    // 当前选中的角色图标
    private RoleChooseIcon selectedRoleIcon;

    // 模型控制器
    private RoleModelController modelController;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override async void Show()
    {
        base.Show();
        // 加载角色数据
        await LoadRoleData();
        // 默认选中女性按钮
        SelectGender(false);
    }

    private async Task LoadRoleData()
    {
        // 加载 RoleListData
        roleListData = await GameMgr.AssetLoader.LoadAsset<RoleListData>("RoleListData");
        if (roleListData == null || roleListData.roleList == null)
        {
            Debug.LogError("[CreateRolePanel] Failed to load RoleListData!");
            return;
        }

        // 加载 RoleChooseIcon 预制体
        roleChooseIconPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("RoleChooseIcon");
        if (roleChooseIconPrefab == null)
        {
            Debug.LogError("[CreateRolePanel] Failed to load RoleChooseIcon prefab!");
            return;
        }

        // 预加载所有角色模型（在后台加载，切换时会是瞬间的）
        if (modelController != null)
        {
            _ = modelController.PreloadAllRoleModels(roleListData);
        }

        Debug.Log($"[CreateRolePanel] Loaded {roleListData.roleList.Count} roles");
    }

    public override void Init()
    {
        InitUIName();
        InitClick();
    }
    private void InitUIName()
    {
        UIChooseSexButton = transform.Find("Content/ChooseSex/ChooseSexButton");
        UIManButton = transform.Find("ChooseBK/Content/ChooseSexPanel/ManButton");
        UIManSelectIcon = transform.Find("ChooseBK/Content/ChooseSexPanel/ManButton/ManSelectIcon");
        UIWomenButton = transform.Find("ChooseBK/Content/ChooseSexPanel/WomenButton");
        UIWomenSelectIcon = transform.Find("ChooseBK/Content/ChooseSexPanel/WomenButton/WomenSelectIcon");
        UIBackButton = transform.Find("TitleBK/BackButton");
        UICompleteButton = transform.Find("TitleBK/CompleteButton");
        UIModelPosition = transform.Find("ModelPosition");
        UIRoleIconBk = transform.Find("RoleInfoPanel/RoleIconBK");
        UIRoleInfo = transform.Find("RoleInfoPanel/RoleInfo");
        UICreateRolePanelBK = transform.Find("CreateRolePanelBK");
        UIChooseRoleButton = transform.Find("ChooseBK/Content/ChooseRoleButton");
        UIChooseRolePanel = transform.Find("ChooseBK/Content/ChooseRolePanel");

        // 角色图标的容器
        roleIconContainer = UIChooseRolePanel;

        // 获取场景中的模型控制器（单例模式）
        modelController = RoleModelController.Instance;
        if (modelController == null)
        {
            Debug.LogWarning("[CreateRolePanel] RoleModelController not found in scene!");
        }
    }

    private void InitClick()
    {
        if (UIChooseSexButton != null)
            UIChooseSexButton.GetComponent<Button>().onClick.AddListener(OnChooseSexButtonClick);
        if (UIManButton != null)
            UIManButton.GetComponent<Button>().onClick.AddListener(OnManButtonClick);
        if (UIWomenButton != null)
            UIWomenButton.GetComponent<Button>().onClick.AddListener(OnWomenButtonClick);
        if (UIBackButton != null)
            UIBackButton.GetComponent<Button>().onClick.AddListener(OnBackButtonClick);
        if (UICompleteButton != null)
            UICompleteButton.GetComponent<Button>().onClick.AddListener(OnCompleteButtonClick);
    }

    private void OnChooseSexButtonClick()
    {

    }

    private void OnManButtonClick()
    {
        SelectGender(true);
    }

    private void OnWomenButtonClick()
    {
        SelectGender(false);
    }

    /// <summary>
    /// 选择性别
    /// </summary>
    /// <param name="isMale">true=男性，false=女性</param>
    private void SelectGender(bool isMale)
    {
        isMaleSelected = isMale;

        // 更新按钮选中图标显示
        if (UIManSelectIcon != null)
            UIManSelectIcon.gameObject.SetActive(isMale);
        if (UIWomenSelectIcon != null)
            UIWomenSelectIcon.gameObject.SetActive(!isMale);

        // 显示对应性别的角色列表
        RefreshRoleList();
    }

    private void RefreshRoleList()
    {
        if (roleListData == null || roleChooseIconPrefab == null || roleIconContainer == null)
            return;

        // 清空现有图标
        foreach (Transform child in roleIconContainer)
        {
            Destroy(child.gameObject);
        }
        maleRoleIcons.Clear();
        femaleRoleIcons.Clear();

        // 只创建当前选中性别的角色图标
        foreach (var roleData in roleListData.roleList)
        {
            // 只创建当前选中性别的角色
            if ((isMaleSelected && !roleData.roleSex) || (!isMaleSelected && roleData.roleSex))
                continue;

            // 获取资源名称
            string iconName, panelBKName, roleBKName, roleModelName;
            roleListData.GetRoleResourceNames(roleData.roleID, roleData.roleSex, out iconName, out panelBKName, out roleBKName, out roleModelName);

            // 创建图标对象
            GameObject iconObj = Instantiate(roleChooseIconPrefab, roleIconContainer);
            iconObj.name = $"RoleIcon_{roleData.roleID}_{iconName}";
            RoleChooseIcon roleChooseIcon = iconObj.GetComponent<RoleChooseIcon>();

            if (roleChooseIcon != null)
            {
                // 设置角色数据的资源名称
                roleData.roleBKName = roleBKName;
                roleData.createRolePanelBKName = panelBKName;

                // 设置角色数据
                roleChooseIcon.SetData(roleData, roleModelName);

                // 注册点击事件
                roleChooseIcon.onIconClick += OnRoleIconClick;

                // 异步加载图标
                _ = LoadRoleIcon(roleChooseIcon, iconName, () => {
                    // 加载完成后显示
                    roleChooseIcon.SetVisible(true);
                });

                // 根据性别添加到对应列表
                if (roleData.roleSex)
                    maleRoleIcons.Add(roleChooseIcon);
                else
                    femaleRoleIcons.Add(roleChooseIcon);
            }
        }

        // 显示当前选中性别的角色
        UpdateRoleIconsVisibility();
    }

    private async Task LoadRoleIcon(RoleChooseIcon icon, string iconName, System.Action onLoaded = null)
    {
        // 查找 UIRoleIcon
        Transform roleIconTransform = icon.transform.Find("RoleIcon");
        if (roleIconTransform == null)
            return;

        // 从 Addressables 加载图标
        Sprite sprite = await GameMgr.AssetLoader.LoadAsset<Sprite>(iconName);
        if (sprite != null)
        {
            Image image = roleIconTransform.GetComponent<Image>();
            if (image != null)
                image.sprite = sprite;
        }

        // 加载完成回调
        onLoaded?.Invoke();
    }

    private void UpdateRoleIconsVisibility()
    {
        // 先隐藏所有图标
        foreach (Transform child in roleIconContainer)
        {
            RoleChooseIcon icon = child.GetComponent<RoleChooseIcon>();
            if (icon != null)
                icon.SetVisible(false);
            else
                child.gameObject.SetActive(false);
        }

        // 显示对应性别的图标
        var targetList = isMaleSelected ? maleRoleIcons : femaleRoleIcons;
        foreach (var icon in targetList)
        {
            if (icon != null)
                icon.SetVisible(true);
        }

        // 自动选中第一个角色
        if (targetList.Count > 0)
        {
            OnRoleIconClick(targetList[0]);
        }
    }

    /// <summary>
    /// 当角色图标被点击时
    /// </summary>
    private async void OnRoleIconClick(RoleChooseIcon clickedIcon)
    {
        if (clickedIcon == null) return;

        // 取消之前选中的图标
        if (selectedRoleIcon != null)
        {
            selectedRoleIcon.SetSelected(false);
        }

        // 设置新选中的图标
        clickedIcon.SetSelected(true);
        selectedRoleIcon = clickedIcon;

        // 获取角色数据和模型名称
        RoleData roleData = clickedIcon.GetRoleData();
        string roleModelName = clickedIcon.GetRoleModelName();

        Debug.Log($"[CreateRolePanel] Selected role: {roleData?.roleID}, model: {roleModelName}");

        // 更新面板显示
        await UpdateRoleInfoPanel(roleData);

        // 切换模型
        if (modelController != null && roleData != null && !string.IsNullOrEmpty(roleModelName))
        {
            await modelController.SwitchModel(roleData, roleModelName);
        }
    }

    /// <summary>
    /// 更新角色信息面板
    /// </summary>
    private async Task UpdateRoleInfoPanel(RoleData roleData)
    {
        if (roleData == null) return;

        // 设置角色信息文本
        if (UIRoleInfo != null)
        {
            TMP_Text textComp = UIRoleInfo.GetComponent<TMP_Text>();
            if (textComp != null)
            {
                textComp.text = roleData.roleInfo;
            }
        }

        // 加载角色背景图片
        if (UIRoleIconBk != null)
        {
            Image bkImage = UIRoleIconBk.GetComponent<Image>();
            if (bkImage != null && !string.IsNullOrEmpty(roleData.roleBKName))
            {
                Sprite bkSprite = await GameMgr.AssetLoader.LoadAsset<Sprite>(roleData.roleBKName);
                if (bkSprite != null)
                {
                    bkImage.sprite = bkSprite;
                }
            }
        }

        // 加载创建角色面板背景图片
        if (UICreateRolePanelBK != null)
        {
            Image panelBkImage = UICreateRolePanelBK.GetComponent<Image>();
            if (panelBkImage != null && !string.IsNullOrEmpty(roleData.createRolePanelBKName))
            {
                Sprite panelBkSprite = await GameMgr.AssetLoader.LoadAsset<Sprite>(roleData.createRolePanelBKName);
                if (panelBkSprite != null)
                {
                    panelBkImage.sprite = panelBkSprite;
                }
            }
        }
    }

    private async void OnCompleteButtonClick()
    {
        // 点击完成按钮，显示输入角色姓名面板
        // CreateRolePanel 不隐藏，只是覆盖显示
        await GameMgr.UI.ShowPanel<CreateRoleNamePanel>();
    }
    private void OnBackButtonClick()
    {
        // 返回选择角色界面
        GameMgr.UI.HidePanel<CreateRolePanel>(async () =>
        {
            await GameMgr.UI.ShowPanel<ChooseRolePanel>();
        });
    }
}
