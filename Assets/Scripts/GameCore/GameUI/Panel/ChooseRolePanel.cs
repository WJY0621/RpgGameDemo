using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

public class ChooseRolePanel : BasePanel
{
    private const string HoverSoundName = "UI_Click2";
    private const string ClickSoundName = "UI_Hover2";

    private Transform UIBackButton;
    private Transform UIStartButton;
    private Transform UIScrollViewContent;
    private Transform UIRolePanelList;
    private Transform UIAddRoleButton1;
    private Transform UIAddRoleButton2;
    private Transform UIRoleName;
    private Transform UIHighLightIcon;

    // RoleInfoPanel 预制体
    private GameObject roleInfoPanelPrefab;

    // 当前选中的角色面板
    private RoleInfoPanel selectedRolePanel;

    // 模型控制器
    private RoleModelController modelController;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        UIBackButton = transform.Find("BackButton");
        UIStartButton = transform.Find("StartButton");
        UIScrollViewContent = transform.Find("Scroll View/Viewport/Content");
        // 直接使用Content作为角色面板的父容器，确保AddRoleButton在最下面
        UIRolePanelList = UIScrollViewContent;
        UIAddRoleButton1 = transform.Find("Scroll View/Viewport/Content/AddRoleButton1");
        UIAddRoleButton2 = transform.Find("AddRoleButton2");
        UIRoleName = transform.Find("RoleName");
        UIHighLightIcon = transform.Find("HighLightIcon");

        // 注意：不要在这里缓存 RoleModelController.Instance。
        // 打包后脚本 Awake 执行顺序可能与编辑器不同，此时 Instance 可能尚未赋值，
        // 会导致 modelController 永远为 null、模型静默不显示。改为用到时惰性解析。

        InitClick();
        LoadRoleInfoPanelPrefab();
    }

    private async void LoadRoleInfoPanelPrefab()
    {
        // 异步加载 RoleInfoPanel 预制体
        roleInfoPanelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("RoleInfoPanel");
    }

    public override async void Show()
    {
        base.Show();
        // 面板显示时加载存档列表
        await RefreshRoleList();
    }

    private async Task RefreshRoleList()
    {

        // 如果预制体还没加载，先等待加载完成
        if (roleInfoPanelPrefab == null)
        {
            roleInfoPanelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("RoleInfoPanel");
            if (roleInfoPanelPrefab == null)
            {
                return;
            }
        }

        // 如果没有存档列表容器，直接返回
        if (UIRolePanelList == null)
        {
            return;
        }

        // 清空现有列表（只删除RoleInfoPanel，保留AddRoleButton1）
        // 先清除之前的事件注册
        if (selectedRolePanel != null)
        {
            selectedRolePanel.onSelectedChanged -= OnRolePanelSelected;
            selectedRolePanel.onDeleted -= OnRolePanelDeleted;
            selectedRolePanel = null;
        }

        SetRoleName(string.Empty);

        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in UIRolePanelList)
        {
            // 只删除RoleInfoPanel，保留AddRoleButton1
            if (child.gameObject.name.Contains("RoleInfoPanel"))
            {
                // 取消事件注册
                RoleInfoPanel panel = child.GetComponent<RoleInfoPanel>();
                if (panel != null)
                {
                    panel.onSelectedChanged -= OnRolePanelSelected;
                    panel.onDeleted -= OnRolePanelDeleted;
                }
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (GameObject obj in toDestroy)
        {
            Destroy(obj);
        }

        // 获取存档数据
        GameMgr.File.EnsureCurrentGameFileForCurrentAccount();
        var gameFiles = GameMgr.File.GetCurrentAccountGameFiles();

        if (gameFiles != null && gameFiles.Count > 0)
        {
            // 为每个存档创建 RoleInfoPanel
            foreach (var gameFile in gameFiles)
            {
                CreateRoleInfoPanel(gameFile);
            }

            // 创建完所有角色面板后，确保AddRoleButton1在最下面
            if (UIAddRoleButton1 != null)
            {
                UIAddRoleButton1.SetAsLastSibling();
            }

            // 默认选中当前存档对应的角色面板
            string currentFileName = GameMgr.File.gameFileData?.currentGameFileName;
            if (!string.IsNullOrEmpty(currentFileName))
            {
                foreach (Transform child in UIRolePanelList)
                {
                    RoleInfoPanel panel = child.GetComponent<RoleInfoPanel>();
                    if (panel != null && panel.GetGameFile() != null)
                    {
                        if (panel.GetGameFile().fileName == currentFileName)
                        {
                            panel.SetSelected(true);
                            selectedRolePanel = panel;

                            // 自动加载当前存档的模型
                            GameFile currentFile = panel.GetGameFile();
                            SetRoleName(currentFile != null ? currentFile.playerName : string.Empty);
                            SyncAccountDisplayName(currentFile);
                            var controller = GetModelController();
                            if (currentFile != null && controller != null && !string.IsNullOrEmpty(currentFile.roleModelName))
                            {
                                _ = controller.SwitchModel(null, currentFile.roleModelName);
                            }
                            else if (currentFile != null && string.IsNullOrEmpty(currentFile.roleModelName))
                            {
                                Debug.LogWarning($"[ChooseRolePanel] 存档 {currentFile.fileName} 的 roleModelName 为空，无法显示模型预览。");
                            }

                            break;
                        }
                    }
                }
            }
        }
    }

    private void CreateRoleInfoPanel(GameFile gameFile)
    {
        if (UIRolePanelList == null)
        {
            return;
        }

        if (roleInfoPanelPrefab == null)
        {
            return;
        }

        GameObject roleInfoObj = Instantiate(roleInfoPanelPrefab, UIRolePanelList);
        RoleInfoPanel roleInfoPanel = roleInfoObj.GetComponent<RoleInfoPanel>();
        if (roleInfoPanel != null)
        {
            roleInfoPanel.SetData(gameFile, gameFile.playerName, gameFile.createTime);
            // 注册选中状态改变事件
            roleInfoPanel.onSelectedChanged += OnRolePanelSelected;
            // 注册删除事件
            roleInfoPanel.onDeleted += OnRolePanelDeleted;
        }
    }

    // 当角色面板被删除时
    private void OnRolePanelDeleted(RoleInfoPanel panel)
    {
        // 如果删除的是当前选中的面板，清空选中状态并清除模型
        if (selectedRolePanel == panel)
        {
            selectedRolePanel = null;
            // 清除模型显示
            var controller = GetModelController();
            if (controller != null)
            {
                controller.ClearModel();
            }
        }
    }

    // 当角色面板被点击选中时
    private async void OnRolePanelSelected(RoleInfoPanel panel)
    {
        // 取消之前选中的面板
        if (selectedRolePanel != null && selectedRolePanel != panel)
        {
            selectedRolePanel.SetSelected(false);
        }

        // 设置当前面板为选中状态
        panel.SetSelected(true);
        selectedRolePanel = panel;

        // 获取存档并加载模型
        GameFile gameFile = panel.GetGameFile();
        SetRoleName(gameFile != null ? gameFile.playerName : string.Empty);
        SyncAccountDisplayName(gameFile);
        var controller = GetModelController();
        if (gameFile != null && controller != null && !string.IsNullOrEmpty(gameFile.roleModelName))
        {
            await controller.SwitchModel(null, gameFile.roleModelName);
        }
        else if (gameFile != null && string.IsNullOrEmpty(gameFile.roleModelName))
        {
            Debug.LogWarning($"[ChooseRolePanel] 存档 {gameFile.fileName} 的 roleModelName 为空，无法显示模型预览。");
        }
    }

    /// <summary>
    /// 惰性获取模型控制器。Instance 为空时给出明确警告，避免静默失败。
    /// </summary>
    private RoleModelController GetModelController()
    {
        if (modelController == null)
        {
            modelController = RoleModelController.Instance;
            if (modelController == null)
            {
                Debug.LogWarning("[ChooseRolePanel] RoleModelController.Instance 为空，无法显示角色模型预览。" +
                    "请确认菜单场景中存在 RoleModelController 物体，且其 GameObject 处于激活状态。");
            }
        }
        return modelController;
    }

    public void InitClick()
    {
        BindButton(UIBackButton, OnClickBack);
        BindButton(UIAddRoleButton1, OnClickAddRole);
        BindButton(UIAddRoleButton2, OnClickAddRole);
        BindButton(UIStartButton, OnClickStart);
    }

    private void BindButton(Transform buttonTransform, UnityEngine.Events.UnityAction onClick)
    {
        if (buttonTransform == null)
        {
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
        AddButtonSounds(buttonTransform);
    }

    private void AddButtonSounds(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        UIHoverSound hoverSound = buttonTransform.GetComponent<UIHoverSound>();
        if (hoverSound == null)
        {
            hoverSound = buttonTransform.gameObject.AddComponent<UIHoverSound>();
        }

        hoverSound.Configure(HoverSoundName);

        UIClickSound clickSound = buttonTransform.GetComponent<UIClickSound>();
        if (clickSound == null)
        {
            clickSound = buttonTransform.gameObject.AddComponent<UIClickSound>();
        }

        clickSound.Configure(ClickSoundName);
    }
    private async void OnClickBack()
    {
        await GameMgr.UI.SwitchPanelAsync<ChooseRolePanel, GameStartPanel>();
    }

    private async void OnClickAddRole()
    {
        // 进入创建角色界面
        await GameMgr.UI.SwitchPanelAsync<ChooseRolePanel, CreateRolePanel>();
    }
    private async void OnClickStart()
    {
        if (selectedRolePanel == null)
        {
            Debug.LogWarning("请先选择一个角色!");
            return;
        }

        // 获取选中的存档
        GameFile selectedFile = selectedRolePanel.GetGameFile();
        if (selectedFile == null)
        {
            return;
        }

        // 设置当前存档
        GameMgr.File.gameFileData.currentGameFileName = selectedFile.fileName;
        GameMgr.File.ApplyCurrentGameFileToRuntime();
        SyncAccountDisplayName(selectedFile);

        // 保存选中的角色模型名称，用于进入游戏后替换模型
        PlayerModelManager.CurrentRoleModelName = selectedFile.roleModelName;

        LoadingResult result = await GameMgr.Scene.LoadSceneAsync("GameScene");
        if (!result.Success)
        {
            Debug.LogError($"[ChooseRolePanel] Failed to enter game scene: {result.ErrorMessage}");
        }
    }

    public void SetRoleName(string name)
    {
        if (UIRoleName == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(name) ? "未选择" : name;
        string content = $"当前角色：{displayName}";

        Text legacyText = UIRoleName.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = content;
        }

        TextMeshProUGUI tmpText = UIRoleName.GetComponent<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.text = content;
        }
    }

    private void SyncAccountDisplayName(GameFile gameFile)
    {
        if (gameFile == null || string.IsNullOrWhiteSpace(gameFile.playerName))
        {
            return;
        }

        GameMgr.Account?.SetDisplayName(gameFile.playerName);
    }
}
