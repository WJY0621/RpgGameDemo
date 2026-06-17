using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMainPanel : BasePanel
{
    private const string ButtonHoverSoundName = "UI_Hover1";
    private const string ButtonClickSoundName = "UI_Click2";

    [Header("Task Preview")]
    [SerializeField] private TaskDataSO previewTaskDataSO;
    [SerializeField] private int previewTaskID = -1;
    [SerializeField] private int previewStepIndex;

    private Transform uiMessagePanel;
    private Transform uiMessageText;
    private Transform uiMessageContent;
    private Transform uiBagButton;
    private Transform uiEquipButton;
    private Transform uiBuildButton;
    private Transform uiTaskButton;
    private Transform uiFriendButton;
    private Transform uiSettingButton;
    private Transform uiBrokenLineButton;
    private bool lastNetworkButtonVisible;

    private PlayerMainLifeBarController lifeBarController;
    private PlayerMainFlyBarController flyBarController;
    private PlayerMainLiquidController liquidController;
    private PlayerMainTaskController taskController;
    private PlayerMainEquipPreviewController equipPreviewController;
    private PlayerMainBuffController buffController;
    private PlayerMainFriendLifeBarController friendLifeBarController;
    private PlayerMainBossLifeBarController bossLifeBarController;
    private bool initialized;

    public TMP_Text MessageText;
    public CanvasGroup MessagePanel;
    public Transform MessageContentRoot => uiMessageContent;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        lifeBarController?.Tick();
        flyBarController?.Tick();
        liquidController?.Tick();
        equipPreviewController?.Tick();
        buffController?.Tick();
        friendLifeBarController?.Tick();
        bossLifeBarController?.Tick();
        RefreshBrokenLineButton();
    }

    private void OnDestroy()
    {
        lifeBarController?.Dispose();
        flyBarController?.Dispose();
        liquidController?.Dispose();
        buffController?.Dispose();
        friendLifeBarController?.Dispose();
        bossLifeBarController?.Dispose();

        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
        }

        if (GameMgr.Network != null)
        {
            GameMgr.Network.OnSessionActiveChanged -= HandleNetworkSessionChanged;
        }
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        InitUIName();
        BindUIReferences();
        InitUIEvent();

        lifeBarController = new PlayerMainLifeBarController(transform);
        lifeBarController.Init();

        flyBarController = new PlayerMainFlyBarController(transform);
        flyBarController.Init();

        liquidController = new PlayerMainLiquidController(transform);
        liquidController.Init();

        taskController = new PlayerMainTaskController(transform);
        taskController.Init(previewTaskDataSO, previewTaskID, previewStepIndex);

        equipPreviewController = new PlayerMainEquipPreviewController(transform);
        equipPreviewController.Init();

        buffController = new PlayerMainBuffController(transform);
        buffController.Init();

        friendLifeBarController = new PlayerMainFriendLifeBarController(transform);
        friendLifeBarController.Init();

        bossLifeBarController = new PlayerMainBossLifeBarController(transform);
        bossLifeBarController.Init();

        if (GameMgr.TaskMgr != null)
        {
            taskController.SetTrackedTaskRuntime(GameMgr.TaskMgr.GetTrackedTask());
        }

        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
            GameMgr.Package.OnInventoryChanged += HandleInventoryChanged;
        }

        if (GameMgr.Network != null)
        {
            GameMgr.Network.OnSessionActiveChanged -= HandleNetworkSessionChanged;
            GameMgr.Network.OnSessionActiveChanged += HandleNetworkSessionChanged;
        }

        RefreshBrokenLineButton(true);
    }

    private void InitUIName()
    {
        uiMessagePanel = transform.Find("MessagePanel");
        uiMessageText = transform.Find("MessagePanel/MessageText");
        uiMessageContent = transform.Find("MessageContent");
        uiBagButton = transform.Find("Button/BagButton");
        uiEquipButton = transform.Find("Button/EquipButton");
        uiBuildButton = transform.Find("Button/BuildButton");
        uiTaskButton = transform.Find("Button/TaskButton");
        uiFriendButton = transform.Find("Button/FriendButton");
        uiSettingButton = transform.Find("Button/SettingButton");
        uiBrokenLineButton = transform.Find("Button/BrokenLineButton");
    }

    private void BindUIReferences()
    {
        if (uiMessageText != null)
        {
            MessageText = uiMessageText.GetComponent<TMP_Text>();
        }

        if (uiMessagePanel != null)
        {
            MessagePanel = uiMessagePanel.GetComponent<CanvasGroup>();
        }
    }

    private void InitUIEvent()
    {
        if (uiBagButton != null)
        {
            Button button = uiBagButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickBag);
            ConfigureButtonSound(uiBagButton);
        }

        if (uiEquipButton != null)
        {
            Button button = uiEquipButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickEquip);
            ConfigureButtonSound(uiEquipButton);
        }

        if (uiBuildButton != null)
        {
            Button button = uiBuildButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickBuild);
            ConfigureButtonSound(uiBuildButton);
        }

        if (uiTaskButton != null)
        {
            Button button = uiTaskButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickTask);
            ConfigureButtonSound(uiTaskButton);
        }

        if (uiFriendButton != null)
        {
            Button button = uiFriendButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickFriend);
            ConfigureButtonSound(uiFriendButton);
            ConfigureRedDot(uiFriendButton, RedDotType.Friend, false);
        }

        if (uiSettingButton != null)
        {
            Button button = uiSettingButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickSetting);
            ConfigureButtonSound(uiSettingButton);
        }

        if (uiBrokenLineButton != null)
        {
            Button button = uiBrokenLineButton.GetComponent<Button>();
            button.onClick.AddListener(OnClickBrokenLine);
            ConfigureButtonSound(uiBrokenLineButton);
        }
    }

    private void ConfigureButtonSound(Transform buttonTransform)
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

        hoverSound.Configure(ButtonHoverSoundName);

        UIClickSound clickSound = buttonTransform.GetComponent<UIClickSound>();
        if (clickSound == null)
        {
            clickSound = buttonTransform.gameObject.AddComponent<UIClickSound>();
        }

        clickSound.Configure(ButtonClickSoundName);
    }

    private void ConfigureRedDot(Transform targetTransform, RedDotType type, bool showNumber)
    {
        if (targetTransform == null)
        {
            return;
        }

        if (targetTransform.Find("RedDot") == null)
        {
            return;
        }

        RedDotView redDotView = targetTransform.GetComponent<RedDotView>();
        if (redDotView == null)
        {
            redDotView = targetTransform.gameObject.AddComponent<RedDotView>();
        }

        redDotView.Configure(type, showNumber, false, true);
    }

    public void SetTrackedTask(TaskDataSO taskDataSO, int taskID, int stepIndex = 0)
    {
        previewTaskDataSO = taskDataSO;
        previewTaskID = taskID;
        previewStepIndex = Mathf.Max(0, stepIndex);
        taskController?.SetTrackedTask(taskDataSO, taskID, stepIndex);
    }

    public void SetTrackedTaskRuntime(TaskRuntime runtime)
    {
        taskController?.SetTrackedTaskRuntime(runtime);
    }

    private void HandleInventoryChanged()
    {
        equipPreviewController?.Refresh();
        liquidController?.Refresh();
    }

    private async void OnClickSetting()
    {
        await GameMgr.UI.ShowPanel<GameSettingPanel>();
    }

    private async void OnClickTask()
    {
        await GameMgr.UI.ShowPanel<TaskPanel>();
    }

    private async void OnClickFriend()
    {
        await GameMgr.UI.ShowPanel<FriendPanel>();
    }

    private async void OnClickBrokenLine()
    {
        if (GameMgr.Network == null || !GameMgr.Network.IsSessionActive)
        {
            RefreshBrokenLineButton(true);
            return;
        }

        await GameMgr.Network.DisconnectToSinglePlayerAsync();
        RefreshBrokenLineButton(true);
    }

    private async void OnClickEquip()
    {
        await GameMgr.UI.ShowPanel<EquipPanel>();
    }

    private async void OnClickBuild()
    {
        if (GameMgr.Build != null)
        {
            GameMgr.Build.SetBuildMenuOpen(true);
            return;
        }

        await GameMgr.UI.ShowPanel<BuildPanel>();
    }

    private async void OnClickBag()
    {
        await GameMgr.UI.ShowPanel<PackagePanel>();
    }

    private void HandleNetworkSessionChanged(bool _)
    {
        RefreshBrokenLineButton(true);
    }

    private void RefreshBrokenLineButton(bool force = false)
    {
        if (uiBrokenLineButton == null)
        {
            return;
        }

        bool visible = GameMgr.Network != null && GameMgr.Network.IsSessionActive;
        if (!force && visible == lastNetworkButtonVisible)
        {
            return;
        }

        lastNetworkButtonVisible = visible;
        uiBrokenLineButton.gameObject.SetActive(visible);
    }
}
