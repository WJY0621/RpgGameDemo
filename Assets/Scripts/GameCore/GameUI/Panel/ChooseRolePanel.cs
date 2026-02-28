using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class ChooseRolePanel : BasePanel
{
    private Transform UIBackButton;
    private Transform UIStartButton;
    private Transform UIRoleMod;
    private Transform UIScrollViewContent;
    private Transform UIRolePanelList;
    private Transform UIAddRoleButton1;
    private Transform UIAddRoleButton2;
    private Transform UIRoleName;

    // RoleInfoPanel 预制体
    private GameObject roleInfoPanelPrefab;

    // 当前选中的角色面板
    private RoleInfoPanel selectedRolePanel;

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
        // 直接使用Content作为角色面板的父容器，确保AddRoleButton在最下面
        UIRolePanelList = UIScrollViewContent;
        UIAddRoleButton1 = transform.Find("Scroll View/Viewport/Content/AddRoleButton1");
        UIAddRoleButton2 = transform.Find("AddRoleButton2");
        UIRoleName = transform.Find("RoleName");
        InitClick();
        LoadRoleInfoPanelPrefab();
    }

    private async void LoadRoleInfoPanelPrefab()
    {
        // 异步加载 RoleInfoPanel 预制体
        roleInfoPanelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("RoleInfoPanel");
        if (roleInfoPanelPrefab == null)
        {
            Debug.LogError("Failed to load RoleInfoPanel prefab!");
        }
    }

    public override async void Show()
    {
        Debug.Log("[ChooseRolePanel] Show() called");
        base.Show();
        // 面板显示时加载存档列表
        await RefreshRoleList();
    }

    private async Task RefreshRoleList()
    {
        Debug.Log("[ChooseRolePanel] RefreshRoleList called");

        // 如果预制体还没加载，先等待加载完成
        if (roleInfoPanelPrefab == null)
        {
            Debug.Log("[ChooseRolePanel] Waiting for RoleInfoPanel prefab to load...");
            roleInfoPanelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>("RoleInfoPanel");
            if (roleInfoPanelPrefab == null)
            {
                Debug.LogError("[ChooseRolePanel] Failed to load RoleInfoPanel prefab!");
                return;
            }
            Debug.Log("[ChooseRolePanel] RoleInfoPanel prefab loaded successfully!");
        }

        // 如果没有存档列表容器，直接返回
        if (UIRolePanelList == null)
        {
            Debug.LogError("[ChooseRolePanel] UIRolePanelList is null!");
            return;
        }

        // 清空现有列表（只删除RoleInfoPanel，保留AddRoleButton1）
        // 先清除之前的事件注册
        if (selectedRolePanel != null)
        {
            selectedRolePanel.onSelectedChanged -= OnRolePanelSelected;
            selectedRolePanel = null;
        }

        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in UIRolePanelList)
        {
            // 只删除RoleInfoPanel，保留AddRoleButton1
            if (child.gameObject.name.Contains("RoleInfoPanel"))
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (GameObject obj in toDestroy)
        {
            Destroy(obj);
        }

        // 获取存档数据
        var gameFiles = GameMgr.File.gameFileData?.gameFiles;
        Debug.Log($"[ChooseRolePanel] gameFiles count: {gameFiles?.Count ?? 0}");

        if (gameFiles != null && gameFiles.Count > 0)
        {
            // 为每个存档创建 RoleInfoPanel
            foreach (var gameFile in gameFiles)
            {
                Debug.Log($"[ChooseRolePanel] Creating role panel for: {gameFile.playerName}");
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
                            Debug.Log($"[ChooseRolePanel] Auto selected: {panel.GetGameFile().playerName}");
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[ChooseRolePanel] No game files found!");
        }
    }

    private void CreateRoleInfoPanel(GameFile gameFile)
    {
        Debug.Log($"[ChooseRolePanel] CreateRoleInfoPanel called for: {gameFile.playerName}");

        if (UIRolePanelList == null)
        {
            Debug.LogError("[ChooseRolePanel] UIRolePanelList is null in CreateRoleInfoPanel!");
            return;
        }

        if (roleInfoPanelPrefab == null)
        {
            Debug.LogWarning("[ChooseRolePanel] roleInfoPanelPrefab is null, skipping instantiation!");
            return;
        }

        GameObject roleInfoObj = Instantiate(roleInfoPanelPrefab, UIRolePanelList);
        RoleInfoPanel roleInfoPanel = roleInfoObj.GetComponent<RoleInfoPanel>();
        if (roleInfoPanel != null)
        {
            roleInfoPanel.SetData(gameFile, gameFile.playerName, gameFile.createTime);
            // 注册选中状态改变事件
            roleInfoPanel.onSelectedChanged += OnRolePanelSelected;
        }
    }

    // 当角色面板被点击选中时
    private void OnRolePanelSelected(RoleInfoPanel panel)
    {
        // 取消之前选中的面板
        if (selectedRolePanel != null && selectedRolePanel != panel)
        {
            selectedRolePanel.SetSelected(false);
        }

        // 设置当前面板为选中状态
        panel.SetSelected(true);
        selectedRolePanel = panel;

        Debug.Log($"[ChooseRolePanel] Selected role: {panel.GetGameFile().playerName}");
    }

    public void InitClick()
    {
        if (UIBackButton != null)
            UIBackButton.GetComponent<Button>().onClick.AddListener(OnClickBack);
        if (UIAddRoleButton1 != null)
            UIAddRoleButton1.GetComponent<Button>().onClick.AddListener(OnClickAddRole);
        if (UIAddRoleButton2 != null)
            UIAddRoleButton2.GetComponent<Button>().onClick.AddListener(OnClickAddRole);
        if (UIStartButton != null)
            UIStartButton.GetComponent<Button>().onClick.AddListener(OnClickStart);
    }
    private void OnClickBack()
    {
        GameMgr.UI.HidePanel<ChooseRolePanel>();
    }
    private void OnClickAddRole()
    {
        // 进入创建角色界面
        GameMgr.UI.HidePanel<ChooseRolePanel>(async () =>
        {
            await GameMgr.UI.ShowPanel<CreateRolePanel>();
        });
    }
    private void OnClickStart()
    {
        if (selectedRolePanel == null)
        {
            Debug.LogWarning("[ChooseRolePanel] 请先选择一个角色!");
            return;
        }

        // 获取选中的存档
        GameFile selectedFile = selectedRolePanel.GetGameFile();
        if (selectedFile == null)
        {
            Debug.LogError("[ChooseRolePanel] 选中的角色存档为空!");
            return;
        }

        Debug.Log($"[ChooseRolePanel] 开始游戏，加载存档: {selectedFile.fileName}");

        // 设置当前存档
        GameMgr.File.gameFileData.currentGameFileName = selectedFile.fileName;

        // 销毁所有UI面板，进入游戏场景
        GameMgr.UI.DestroyAllPanels();
        GameMgr.Scene.LoadSceneAsync("GameScene").Forget();
    }

    public void SetRoleName(string name)
    {
        UIRoleName.GetComponent<Text>().text = name;
    }
}
