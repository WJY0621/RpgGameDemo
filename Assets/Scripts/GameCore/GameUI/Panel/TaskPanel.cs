using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskPanel : BasePanel
{
    [Header("Optional Data")]
    [SerializeField] private TaskDataSO fallbackTaskDataSO;

    private bool initialized;

    private Transform uiCloseButton;
    private Transform uiTraceButton;
    private Transform taskListContent;
    private Transform taskTargetRoot;
    private Transform awardContentRoot;
    private GameObject awardGoldRoot;

    private TaskNameItem taskNameItemTemplate;
    private GameObject taskTargetItemTemplate;
    private GameObject awardItemTemplate;

    private TMP_Text taskNameText;
    private TMP_Text taskDescriptionText;
    private TMP_Text taskTargetTitleText;
    private TMP_Text awardTitleText;
    private TMP_Text awardGoldNumberText;

    private readonly List<TaskNameItem> spawnedTaskNameItems = new List<TaskNameItem>();
    private readonly List<GameObject> spawnedTaskTargetItems = new List<GameObject>();
    private readonly List<GameObject> spawnedAwardItems = new List<GameObject>();

    private TaskRuntime selectedTaskRuntime;
    private int rewardRefreshVersion;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    private void OnEnable()
    {
        SubscribeTaskEvents();
        RefreshTaskList();
    }

    private void OnDisable()
    {
        UnsubscribeTaskEvents();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        CacheUI();
        CacheTemplates();
        BindButtons();
        ClearDetail();
        RefreshTaskList();
    }

    public override void Show()
    {
        base.Show();
        RefreshTaskList();
    }

    private void CacheUI()
    {
        uiCloseButton = transform.Find("BK/CloseButton");
        uiTraceButton = transform.Find("BK/TraceButton");
        taskListContent = transform.Find("BK/TaskListBK/Scroll View/Viewport/Content");
        taskTargetRoot = transform.Find("BK/TaskContentBK/TaskTarget");
        awardContentRoot = transform.Find("BK/TaskContentBK/AwardContent");

        Transform taskContentRoot = transform.Find("BK/TaskContentBK");
        if (taskContentRoot != null)
        {
            taskNameText = FindText(taskContentRoot, "TaskName");
            taskDescriptionText = FindText(taskContentRoot, "TaskDescription");
            taskTargetTitleText = FindText(taskContentRoot, "TaskTarget");
            awardTitleText = FindText(taskContentRoot, "AwardContent");
            awardGoldNumberText = FindText(taskContentRoot, "GoldNumbel");

            Transform awardGoldTransform = taskContentRoot.Find("AwardGoldIcon");
            awardGoldRoot = awardGoldTransform != null ? awardGoldTransform.gameObject : null;
        }
    }

    private void CacheTemplates()
    {
        if (taskListContent != null)
        {
            taskNameItemTemplate = taskListContent.GetComponentInChildren<TaskNameItem>(true);
            if (taskNameItemTemplate == null)
            {
                Transform templateTransform = taskListContent.Find("TaskNameItem");
                if (templateTransform != null)
                {
                    taskNameItemTemplate = templateTransform.gameObject.GetComponent<TaskNameItem>();
                    if (taskNameItemTemplate == null)
                    {
                        taskNameItemTemplate = templateTransform.gameObject.AddComponent<TaskNameItem>();
                    }
                }
            }

            if (taskNameItemTemplate != null)
            {
                taskNameItemTemplate.gameObject.SetActive(false);
            }
        }

        if (taskTargetRoot != null)
        {
            Transform template = taskTargetRoot.Find("TaskTargetitem");
            if (template != null)
            {
                taskTargetItemTemplate = template.gameObject;
                taskTargetItemTemplate.SetActive(false);
            }
        }

        if (awardContentRoot != null)
        {
            Transform template = awardContentRoot.Find("AwardItem");
            if (template != null)
            {
                awardItemTemplate = template.gameObject;
                awardItemTemplate.SetActive(false);
            }
        }
    }

    private void BindButtons()
    {
        if (uiCloseButton != null)
        {
            Button closeButton = GetButton(uiCloseButton);
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnClickClose);
                closeButton.onClick.AddListener(OnClickClose);
            }
        }
    }

    private void SubscribeTaskEvents()
    {
        if (GameMgr.TaskMgr == null)
        {
            return;
        }

        GameMgr.TaskMgr.OnTaskAccepted -= HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskAccepted += HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskUpdated -= HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskUpdated += HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskCompleted -= HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskCompleted += HandleTaskChanged;
    }

    private void UnsubscribeTaskEvents()
    {
        if (GameMgr.TaskMgr == null)
        {
            return;
        }

        GameMgr.TaskMgr.OnTaskAccepted -= HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskUpdated -= HandleTaskChanged;
        GameMgr.TaskMgr.OnTaskCompleted -= HandleTaskChanged;
    }

    private void HandleTaskChanged(TaskRuntime runtime)
    {
        RefreshTaskList();
    }

    private void RefreshTaskList()
    {
        if (!initialized || taskListContent == null)
        {
            return;
        }

        ClearSpawnedTaskNameItems();

        List<TaskRuntime> activeTasks = new List<TaskRuntime>();
        if (GameMgr.TaskMgr != null)
        {
            activeTasks = GameMgr.TaskMgr.AcceptedTasks.Values
                .Where(runtime => runtime != null && !runtime.IsCompleted())
                .OrderBy(runtime => runtime.taskID)
                .ToList();
        }

        foreach (TaskRuntime runtime in activeTasks)
        {
            CreateTaskNameItem(runtime);
        }

        if (activeTasks.Count == 0)
        {
            selectedTaskRuntime = null;
            ClearDetail();
            return;
        }

        TaskRuntime targetSelection = activeTasks.FirstOrDefault(runtime => selectedTaskRuntime != null && runtime.taskID == selectedTaskRuntime.taskID);
        if (targetSelection == null)
        {
            targetSelection = activeTasks[0];
        }

        SelectTask(targetSelection);
    }

    private void CreateTaskNameItem(TaskRuntime runtime)
    {
        if (taskNameItemTemplate == null || runtime == null)
        {
            return;
        }

        GameObject itemObject = Instantiate(taskNameItemTemplate.gameObject, taskListContent);
        itemObject.name = $"TaskNameItem_{runtime.taskID}";
        itemObject.SetActive(true);

        TaskNameItem item = itemObject.GetComponent<TaskNameItem>();
        if (item == null)
        {
            item = itemObject.AddComponent<TaskNameItem>();
        }

        item.Bind(this, runtime);
        spawnedTaskNameItems.Add(item);
    }

    public void SelectTask(TaskRuntime runtime)
    {
        selectedTaskRuntime = runtime;

        for (int i = 0; i < spawnedTaskNameItems.Count; i++)
        {
            TaskNameItem item = spawnedTaskNameItems[i];
            bool isSelected = item != null && runtime != null && item.TaskID == runtime.taskID;
            item?.SetSelected(isSelected);
        }

        RefreshTaskDetail(runtime);
    }

    private void RefreshTaskDetail(TaskRuntime runtime)
    {
        if (runtime == null)
        {
            ClearDetail();
            return;
        }

        TaskData taskData = ResolveTaskData(runtime);
        string detailTaskName = taskData != null ? taskData.taskName : runtime.taskName;
        string detailDescription = taskData != null ? taskData.taskDescription : runtime.taskDescription;

        if (taskNameText != null)
        {
            taskNameText.text = detailTaskName;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = detailDescription;
        }

        RefreshTaskTargets(runtime);
        RefreshRewards(runtime);
    }

    private void RefreshTaskTargets(TaskRuntime runtime)
    {
        ClearSpawnedTargetItems();

        if (taskTargetRoot == null || taskTargetItemTemplate == null || runtime == null)
        {
            return;
        }

        TaskStepRuntime currentStep = runtime.GetCurrentStep();
        if (currentStep == null)
        {
            return;
        }

        GameObject targetItem = Instantiate(taskTargetItemTemplate, taskTargetRoot);
        targetItem.name = $"TaskTargetItem_{runtime.taskID}";
        targetItem.SetActive(true);

        TMP_Text text = targetItem.GetComponent<TMP_Text>();
        if (text == null)
        {
            text = targetItem.GetComponentInChildren<TMP_Text>(true);
        }

        if (text != null)
        {
            text.text = currentStep.BuildDisplayText();
        }

        spawnedTaskTargetItems.Add(targetItem);
    }

    private void RefreshRewards(TaskRuntime runtime)
    {
        ClearSpawnedAwardItems();
        SetGoldReward(0);
        int refreshVersion = rewardRefreshVersion;

        List<TaskRewardData> rewards = FindNextPendingRewards(runtime);
        if (rewards.Count == 0)
        {
            return;
        }

        int goldAmount = 0;
        for (int i = 0; i < rewards.Count; i++)
        {
            TaskRewardData reward = rewards[i];
            if (reward.rewardType == TaskRewardType.Gold)
            {
                goldAmount += Mathf.Max(0, reward.amount);
            }
        }

        SetGoldReward(goldAmount);

        if (awardContentRoot == null || awardItemTemplate == null)
        {
            return;
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            TaskRewardData reward = rewards[i];
            if (reward.rewardType != TaskRewardType.Gold)
            {
                CreateAwardItem(reward, refreshVersion);
            }
        }
    }

    private static List<TaskRewardData> FindNextPendingRewards(TaskRuntime runtime)
    {
        if (runtime == null || runtime.IsCompleted() || runtime.stepRuntimes == null)
        {
            return new List<TaskRewardData>();
        }

        for (int i = 0; i < runtime.stepRuntimes.Count; i++)
        {
            TaskStepRuntime stepRuntime = runtime.stepRuntimes[i];
            if (stepRuntime == null || stepRuntime.stepRewardsGranted || stepRuntime.stepRewardList == null)
            {
                continue;
            }

            List<TaskRewardData> rewards = stepRuntime.stepRewardList
                .Where(reward => reward != null)
                .ToList();
            if (rewards.Count > 0)
            {
                return rewards;
            }
        }

        return new List<TaskRewardData>();
    }

    private void SetGoldReward(int amount)
    {
        bool hasGold = amount > 0;
        if (awardGoldRoot != null)
        {
            awardGoldRoot.SetActive(hasGold);
        }

        if (awardGoldNumberText != null)
        {
            awardGoldNumberText.text = Mathf.Max(0, amount).ToString();
        }
    }

    private async void CreateAwardItem(TaskRewardData rewardData, int refreshVersion)
    {
        GameObject itemObject = Instantiate(awardItemTemplate, awardContentRoot);
        itemObject.name = $"AwardItem_{rewardData.rewardType}";
        itemObject.SetActive(true);
        spawnedAwardItems.Add(itemObject);

        Transform iconTransform = itemObject.transform.Find("ItemIcon");
        Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        TMP_Text awardNameText = FindText(itemObject.transform, "AwardItemName");

        if (GameMgr.Package != null)
        {
            await GameMgr.Package.Init();
        }

        if (!IsCurrentAwardItem(itemObject, refreshVersion))
        {
            return;
        }

        if (awardNameText != null)
        {
            awardNameText.text = BuildRewardDisplayName(rewardData);
        }

        await TryApplyRewardIcon(itemObject, iconImage, rewardData, refreshVersion);
    }

    private async Cysharp.Threading.Tasks.UniTask TryApplyRewardIcon(
        GameObject itemObject,
        Image iconImage,
        TaskRewardData rewardData,
        int refreshVersion)
    {
        if (!IsCurrentAwardItem(itemObject, refreshVersion) || iconImage == null || rewardData == null)
        {
            return;
        }

        iconImage.enabled = false;

        if (rewardData.rewardType != TaskRewardType.Item && rewardData.rewardType != TaskRewardType.Equipment)
        {
            return;
        }

        if (GameMgr.Package == null)
        {
            return;
        }

        await GameMgr.Package.Init();
        if (!IsCurrentAwardItem(itemObject, refreshVersion) || iconImage == null)
        {
            return;
        }

        int configID = rewardData.rewardType == TaskRewardType.Item ? rewardData.itemID : rewardData.equipmentID;
        Item itemConfig = GameMgr.Package.GetItemConfig(configID);
        if (itemConfig == null || string.IsNullOrWhiteSpace(itemConfig.iconName))
        {
            return;
        }

        Sprite iconSprite = await GameMgr.IconAtlas.GetItemIcon(itemConfig);
        if (!IsCurrentAwardItem(itemObject, refreshVersion) || iconImage == null || iconSprite == null)
        {
            return;
        }

        iconImage.sprite = iconSprite;
        iconImage.enabled = true;
    }

    private bool IsCurrentAwardItem(GameObject itemObject, int refreshVersion)
    {
        return refreshVersion == rewardRefreshVersion
            && itemObject != null
            && spawnedAwardItems.Contains(itemObject);
    }

    private TaskData ResolveTaskData(TaskRuntime runtime)
    {
        if (runtime == null)
        {
            return null;
        }

        if (runtime.sourceTaskDataSO != null)
        {
            TaskData data = runtime.sourceTaskDataSO.GetTaskData(runtime.taskID);
            if (data != null)
            {
                return data;
            }
        }

        if (fallbackTaskDataSO != null)
        {
            return fallbackTaskDataSO.GetTaskData(runtime.taskID);
        }

        return null;
    }

    private string BuildStepDisplayText(TaskStepRuntime stepRuntime)
    {
        if (stepRuntime == null)
        {
            return string.Empty;
        }

        string baseText = string.IsNullOrWhiteSpace(stepRuntime.stepDescription)
            ? stepRuntime.stepName
            : stepRuntime.stepDescription;

        switch (stepRuntime.stepType)
        {
            case TaskStepType.Kill:
            case TaskStepType.Collect:
                return $"{baseText} ({stepRuntime.currentCount}/{stepRuntime.targetCount})";
            default:
                return baseText;
        }
    }

    private string BuildRewardDisplayName(TaskRewardData rewardData)
    {
        if (rewardData == null)
        {
            return string.Empty;
        }

        switch (rewardData.rewardType)
        {
            case TaskRewardType.Gold:
                return $"金币 x{rewardData.amount}";
            case TaskRewardType.Item:
                return BuildItemRewardText(rewardData.itemID, rewardData.amount, "物品");
            case TaskRewardType.Equipment:
                return BuildItemRewardText(rewardData.equipmentID, rewardData.amount, "装备");
            case TaskRewardType.UnlockTask:
                return $"解锁任务 {rewardData.unlockTaskID}";
            case TaskRewardType.Custom:
                return string.IsNullOrWhiteSpace(rewardData.customRewardID)
                    ? $"自定义奖励 x{rewardData.amount}"
                    : $"{rewardData.customRewardID} x{rewardData.amount}";
            default:
                return rewardData.rewardType.ToString();
        }
    }

    private string BuildItemRewardText(int itemID, int amount, string fallbackPrefix)
    {
        Item itemConfig = GameMgr.Package != null ? GameMgr.Package.GetItemConfig(itemID) : null;
        string itemName = itemConfig != null ? itemConfig.name : $"{fallbackPrefix}{itemID}";
        return $"{itemName} x{amount}";
    }

    private void ClearDetail()
    {
        if (taskNameText != null)
        {
            taskNameText.text = string.Empty;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = string.Empty;
        }

        ClearSpawnedTargetItems();
        ClearSpawnedAwardItems();
        SetGoldReward(0);
    }

    private void ClearSpawnedTaskNameItems()
    {
        for (int i = 0; i < spawnedTaskNameItems.Count; i++)
        {
            if (spawnedTaskNameItems[i] != null)
            {
                Destroy(spawnedTaskNameItems[i].gameObject);
            }
        }

        spawnedTaskNameItems.Clear();
    }

    private void ClearSpawnedTargetItems()
    {
        for (int i = 0; i < spawnedTaskTargetItems.Count; i++)
        {
            if (spawnedTaskTargetItems[i] != null)
            {
                Destroy(spawnedTaskTargetItems[i]);
            }
        }

        spawnedTaskTargetItems.Clear();
    }

    private void ClearSpawnedAwardItems()
    {
        rewardRefreshVersion++;

        for (int i = 0; i < spawnedAwardItems.Count; i++)
        {
            if (spawnedAwardItems[i] != null)
            {
                Destroy(spawnedAwardItems[i]);
            }
        }

        spawnedAwardItems.Clear();
    }

    private TMP_Text FindText(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root.GetComponent<TMP_Text>();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            TMP_Text result = FindText(root.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private Button GetButton(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        return button != null ? button : target.GetComponentInChildren<Button>(true);
    }

    private void OnClickClose()
    {
        GameMgr.UI?.HidePanel<TaskPanel>();
    }
}


