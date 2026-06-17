using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class TaskManager
{
    private const float RuntimeUpdateSaveDebounceSeconds = 0.75f;

    private readonly Dictionary<int, TaskRuntime> acceptedTasks = new Dictionary<int, TaskRuntime>();
    private readonly HashSet<int> completedTaskIDs = new HashSet<int>();

    private int trackedTaskID = -1;
    private bool runtimeSaveQueued;

    public event Action<TaskRuntime> OnTaskAccepted;
    public event Action<TaskRuntime> OnTaskUpdated;
    public event Action<TaskRuntime> OnTaskCompleted;
    public event Action<TaskRuntime> OnTrackedTaskChanged;
    public event Action<TaskRuntime, TaskStepRuntime, TaskStepRuntime> OnTaskStepChanged;

    public IReadOnlyDictionary<int, TaskRuntime> AcceptedTasks => acceptedTasks;
    public IReadOnlyCollection<int> CompletedTaskIDs => completedTaskIDs;

    private const string MonsterKilledEventName = "MonsterKilled";

    public TaskManager()
    {
        // 任务推进走事件总线：怪物死亡时广播 MonsterKilled 事件，这里订阅后转交给推进逻辑。
        GameMgr.Event?.Register(MonsterKilledEventName,
            new GameEventOneParam<int>(new GameActionOneParam<int>(OnMonsterKilledBroadcast)));
    }

    private void OnMonsterKilledBroadcast(int monsterID)
    {
        NotifyMonsterKilled(monsterID);
    }

    public void Clear()
    {
        acceptedTasks.Clear();
        completedTaskIDs.Clear();
        trackedTaskID = -1;
        RefreshTrackedTaskUI();
    }

    public TaskRuntime AcceptTask(TaskDataSO taskDataSO, int taskID)
    {
        if (taskDataSO == null)
        {
            Debug.LogWarning("[TaskManager] TaskDataSO is null.");
            return null;
        }

        TaskData taskData = taskDataSO.GetTaskData(taskID);
        if (taskData != null && taskData.prerequisiteTaskIDs != null)
        {
            for (int i = 0; i < taskData.prerequisiteTaskIDs.Count; i++)
            {
                int prerequisiteTaskID = taskData.prerequisiteTaskIDs[i];
                if (!completedTaskIDs.Contains(prerequisiteTaskID))
                {
                    Debug.Log($"[TaskManager] Task {taskID} is locked by prerequisite task {prerequisiteTaskID}.");
                    return null;
                }
            }
        }

        if (completedTaskIDs.Contains(taskID))
        {
            Debug.Log($"[TaskManager] Task {taskID} already completed.");
            return acceptedTasks.ContainsKey(taskID) ? acceptedTasks[taskID] : null;
        }

        if (acceptedTasks.TryGetValue(taskID, out TaskRuntime existingRuntime))
        {
            TrackTask(taskID);
            return existingRuntime;
        }

        TaskRuntime runtime = taskDataSO.CreateRuntime(taskID);
        if (runtime == null)
        {
            Debug.LogWarning($"[TaskManager] Cannot create runtime for taskID={taskID}");
            return null;
        }

        acceptedTasks.Add(taskID, runtime);
        TrackTask(taskID);

        OnTaskAccepted?.Invoke(runtime);
        NotifyTaskStepChanged(runtime, null, runtime.GetCurrentStep());
        OnTaskUpdated?.Invoke(runtime);
        RefreshTrackedTaskUI();
        QueueRuntimeSave();
        return runtime;
    }

    public bool TrackTask(int taskID)
    {
        if (!acceptedTasks.ContainsKey(taskID))
        {
            return false;
        }

        trackedTaskID = taskID;
        TaskRuntime trackedRuntime = acceptedTasks[trackedTaskID];
        OnTrackedTaskChanged?.Invoke(trackedRuntime);
        RefreshTrackedTaskUI();
        QueueRuntimeSave();
        return true;
    }

    public TaskRuntime GetTrackedTask()
    {
        if (trackedTaskID < 0 || !acceptedTasks.ContainsKey(trackedTaskID))
        {
            return null;
        }

        TaskRuntime runtime = acceptedTasks[trackedTaskID];
        if (runtime == null || runtime.IsCompleted())
        {
            return null;
        }

        return runtime;
    }

    public bool HasAcceptedTask(int taskID)
    {
        return acceptedTasks.ContainsKey(taskID);
    }

    public bool IsTaskAvailable(int taskID)
    {
        if (HasAcceptedTask(taskID) || IsTaskCompleted(taskID))
        {
            return false;
        }

        TaskData taskData = ResolveTaskData(taskID);
        if (taskData == null || taskData.prerequisiteTaskIDs == null || taskData.prerequisiteTaskIDs.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < taskData.prerequisiteTaskIDs.Count; i++)
        {
            if (!completedTaskIDs.Contains(taskData.prerequisiteTaskIDs[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsTaskInProgress(int taskID)
    {
        if (!acceptedTasks.TryGetValue(taskID, out TaskRuntime runtime) || runtime == null)
        {
            return false;
        }

        return !runtime.IsCompleted();
    }

    public bool IsTaskAtStep(int taskID, int stepID)
    {
        if (stepID < 0)
        {
            return false;
        }

        if (!acceptedTasks.TryGetValue(taskID, out TaskRuntime runtime) || runtime == null || runtime.IsCompleted())
        {
            return false;
        }

        TaskStepRuntime currentStep = runtime.GetCurrentStep();
        return currentStep != null && currentStep.stepID == stepID;
    }

    public bool IsTaskCompleted(int taskID)
    {
        return completedTaskIDs.Contains(taskID);
    }

    public void NotifyNpcDialogue(string npcID)
    {
        if (string.IsNullOrWhiteSpace(npcID))
        {
            return;
        }

        foreach (TaskRuntime runtime in acceptedTasks.Values)
        {
            if (runtime == null || runtime.IsCompleted())
            {
                continue;
            }

            int previousStepIndex = runtime.currentStepIndex;
            TaskStepRuntime previousStep = runtime.GetCurrentStep();
            if (!runtime.TryHandleNpcDialogue(npcID))
            {
                continue;
            }

            HandleRuntimeUpdated(runtime, previousStep, previousStepIndex);
        }
    }

    public bool CompleteDialogueTask(int taskID)
    {
        return CompleteDialogueTask(taskID, -1);
    }

    public bool CompleteDialogueTask(int taskID, int stepID)
    {
        if (!acceptedTasks.TryGetValue(taskID, out TaskRuntime runtime) || runtime == null || runtime.IsCompleted())
        {
            return false;
        }

        if (stepID <= 0)
        {
            return false;
        }

        TaskStepRuntime currentStep = runtime.GetCurrentStep();
        if (currentStep == null || currentStep.stepType != TaskStepType.Dialogue)
        {
            string stepInfo = currentStep != null ? $"{currentStep.stepID}/{currentStep.stepType}" : "null";
            return false;
        }

        if (currentStep.stepID != stepID)
        {
            return false;
        }

        int previousStepIndex = runtime.currentStepIndex;
        TaskStepRuntime previousStep = currentStep;
        runtime.CompleteCurrentStep();
        HandleRuntimeUpdated(runtime, previousStep, previousStepIndex);
        return true;
    }

    public void NotifyMonsterKilled(int monsterID)
    {
        foreach (TaskRuntime runtime in acceptedTasks.Values)
        {
            if (runtime == null || runtime.IsCompleted())
            {
                continue;
            }

            int previousStepIndex = runtime.currentStepIndex;
            TaskStepRuntime previousStep = runtime.GetCurrentStep();
            if (!runtime.TryHandleMonsterKilled(monsterID))
            {
                continue;
            }

            HandleRuntimeUpdated(runtime, previousStep, previousStepIndex);
        }
    }

    public TaskSystemSaveData BuildSaveData()
    {
        TaskSystemSaveData saveData = new TaskSystemSaveData
        {
            trackedTaskID = trackedTaskID,
            completedTaskIDs = new List<int>(completedTaskIDs)
        };

        foreach (TaskRuntime runtime in acceptedTasks.Values)
        {
            if (runtime == null)
            {
                continue;
            }

            TaskSaveData taskSave = new TaskSaveData
            {
                taskID = runtime.taskID,
                taskName = runtime.taskName,
                taskDescription = runtime.taskDescription,
                taskType = runtime.taskType,
                giverNpcID = runtime.giverNpcID,
                state = runtime.state,
                currentStepIndex = runtime.currentStepIndex
            };

            if (runtime.stepRuntimes != null)
            {
                foreach (TaskStepRuntime stepRuntime in runtime.stepRuntimes)
                {
                    if (stepRuntime == null)
                    {
                        continue;
                    }

                    TaskStepSaveData stepSave = new TaskStepSaveData
                    {
                        stepID = stepRuntime.stepID,
                        stepName = stepRuntime.stepName,
                        stepDescription = stepRuntime.stepDescription,
                        stepType = stepRuntime.stepType,
                        currentCount = stepRuntime.currentCount,
                        targetCount = stepRuntime.targetCount,
                        isCompleted = stepRuntime.isCompleted,
                        stepRewardsGranted = stepRuntime.stepRewardsGranted,
                        targetNpcID = stepRuntime.targetNpcID,
                        targetMonsterID = stepRuntime.targetMonsterID,
                        targetItemID = stepRuntime.targetItemID,
                        targetAreaID = stepRuntime.targetAreaID,
                        targetDialogueGroup = stepRuntime.targetDialogueGroup,
                        customTargetID = stepRuntime.customTargetID,
                        stepRewardList = CloneRewardList(stepRuntime.stepRewardList)
                    };

                    if (stepRuntime.objectiveRuntimes != null)
                    {
                        foreach (TaskObjectiveRuntime objectiveRuntime in stepRuntime.objectiveRuntimes)
                        {
                            if (objectiveRuntime == null)
                            {
                                continue;
                            }

                            stepSave.objectiveSaveList.Add(new TaskObjectiveSaveData
                            {
                                objectiveID = objectiveRuntime.objectiveID,
                                objectiveType = objectiveRuntime.objectiveType,
                                currentCount = objectiveRuntime.currentCount,
                                targetCount = objectiveRuntime.targetCount,
                                isCompleted = objectiveRuntime.isCompleted,
                                targetNpcID = objectiveRuntime.targetNpcID,
                                targetMonsterID = objectiveRuntime.targetMonsterID,
                                targetItemID = objectiveRuntime.targetItemID,
                                targetAreaID = objectiveRuntime.targetAreaID,
                                targetDialogueGroup = objectiveRuntime.targetDialogueGroup,
                                customTargetID = objectiveRuntime.customTargetID
                            });
                        }
                    }

                    taskSave.stepSaveList.Add(stepSave);
                }
            }

            saveData.acceptedTaskSaves.Add(taskSave);
        }

        return saveData;
    }

    public void LoadFromSaveData(TaskSystemSaveData saveData)
    {
        Clear();

        if (saveData == null)
        {
            return;
        }

        if (saveData.completedTaskIDs != null)
        {
            foreach (int taskID in saveData.completedTaskIDs)
            {
                completedTaskIDs.Add(taskID);
            }
        }

        if (saveData.acceptedTaskSaves != null)
        {
            foreach (TaskSaveData taskSave in saveData.acceptedTaskSaves)
            {
                if (taskSave == null)
                {
                    continue;
                }

                TaskRuntime runtime = new TaskRuntime
                {
                    taskID = taskSave.taskID,
                    taskName = taskSave.taskName,
                    taskDescription = taskSave.taskDescription,
                    taskType = taskSave.taskType,
                    giverNpcID = taskSave.giverNpcID,
                    state = taskSave.state,
                    currentStepIndex = taskSave.currentStepIndex,
                    stepRuntimes = new List<TaskStepRuntime>()
                };

                if (taskSave.stepSaveList != null)
                {
                    foreach (TaskStepSaveData stepSave in taskSave.stepSaveList)
                    {
                        if (stepSave == null)
                        {
                            continue;
                        }

                        TaskStepRuntime stepRuntime = new TaskStepRuntime
                        {
                            stepID = stepSave.stepID,
                            stepName = stepSave.stepName,
                            stepDescription = stepSave.stepDescription,
                            stepType = stepSave.stepType,
                            currentCount = stepSave.currentCount,
                            targetCount = stepSave.targetCount,
                            isCompleted = stepSave.isCompleted,
                            stepRewardsGranted = stepSave.stepRewardsGranted,
                            targetNpcID = stepSave.targetNpcID,
                            targetMonsterID = stepSave.targetMonsterID,
                            targetItemID = stepSave.targetItemID,
                            targetAreaID = stepSave.targetAreaID,
                            targetDialogueGroup = stepSave.targetDialogueGroup,
                            customTargetID = stepSave.customTargetID,
                            objectiveRuntimes = new List<TaskObjectiveRuntime>(),
                            stepRewardList = CloneRewardList(stepSave.stepRewardList)
                        };

                        if (stepSave.objectiveSaveList != null)
                        {
                            foreach (TaskObjectiveSaveData objectiveSave in stepSave.objectiveSaveList)
                            {
                                if (objectiveSave == null)
                                {
                                    continue;
                                }

                                stepRuntime.objectiveRuntimes.Add(new TaskObjectiveRuntime
                                {
                                    objectiveID = objectiveSave.objectiveID,
                                    objectiveType = objectiveSave.objectiveType,
                                    currentCount = objectiveSave.currentCount,
                                    targetCount = objectiveSave.targetCount,
                                    isCompleted = objectiveSave.isCompleted,
                                    targetNpcID = objectiveSave.targetNpcID,
                                    targetMonsterID = objectiveSave.targetMonsterID,
                                    targetItemID = objectiveSave.targetItemID,
                                    targetAreaID = objectiveSave.targetAreaID,
                                    targetDialogueGroup = objectiveSave.targetDialogueGroup,
                                    customTargetID = objectiveSave.customTargetID
                                });
                            }
                        }

                        if (stepRuntime.objectiveRuntimes != null && stepRuntime.objectiveRuntimes.Count > 0)
                        {
                            stepRuntime.RefreshSummaryFromObjectives();
                        }

                        runtime.stepRuntimes.Add(stepRuntime);
                    }
                }

                acceptedTasks[runtime.taskID] = runtime;
            }
        }

        trackedTaskID = saveData.trackedTaskID;
        RefreshTrackedTaskUI();
    }

    private void HandleRuntimeUpdated(TaskRuntime runtime, TaskStepRuntime previousStep = null, int previousStepIndex = -1)
    {
        if (runtime == null)
        {
            return;
        }

        GrantPendingStepRewards(runtime);

        if (runtime.CanSubmit())
        {
            runtime.MarkCompleted();
        }

        if (runtime.IsCompleted())
        {
            completedTaskIDs.Add(runtime.taskID);
            GameMgr.Message?.ShowTaskCompleted(runtime.taskName);
            OnTaskCompleted?.Invoke(runtime);

            if (trackedTaskID == runtime.taskID)
            {
                trackedTaskID = -1;
                OnTrackedTaskChanged?.Invoke(null);
                RefreshTrackedTaskUI();
            }
        }
        else
        {
            NotifyTaskStepChangedIfNeeded(runtime, previousStep, previousStepIndex);
        }

        OnTaskUpdated?.Invoke(runtime);

        if (trackedTaskID == runtime.taskID)
        {
            RefreshTrackedTaskUI();
        }

        QueueRuntimeSave();
    }

    private void NotifyTaskStepChangedIfNeeded(TaskRuntime runtime, TaskStepRuntime previousStep, int previousStepIndex)
    {
        if (runtime == null || previousStepIndex < 0)
        {
            return;
        }

        TaskStepRuntime currentStep = runtime.GetCurrentStep();
        bool stepChanged = runtime.currentStepIndex != previousStepIndex || currentStep != previousStep;
        if (!stepChanged)
        {
            return;
        }

        NotifyTaskStepChanged(runtime, previousStep, currentStep);
    }

    private void NotifyTaskStepChanged(TaskRuntime runtime, TaskStepRuntime previousStep, TaskStepRuntime currentStep)
    {
        OnTaskStepChanged?.Invoke(runtime, previousStep, currentStep);
    }

    private void GrantPendingStepRewards(TaskRuntime runtime)
    {
        if (runtime?.stepRuntimes == null)
        {
            return;
        }

        bool stateChanged = false;

        for (int i = 0; i < runtime.stepRuntimes.Count; i++)
        {
            TaskStepRuntime stepRuntime = runtime.stepRuntimes[i];
            if (stepRuntime == null || !stepRuntime.isCompleted || stepRuntime.stepRewardsGranted)
            {
                continue;
            }

            stepRuntime.stepRewardsGranted = true;
            stateChanged = true;

            if (stepRuntime.stepRewardList != null && stepRuntime.stepRewardList.Count > 0)
            {
                GrantStepRewardsAsync(stepRuntime).Forget();
            }
        }

        if (stateChanged)
        {
            QueueRuntimeSave();
        }
    }

    private async UniTaskVoid GrantStepRewardsAsync(TaskStepRuntime stepRuntime)
    {
        if (stepRuntime?.stepRewardList == null || GameMgr.Package == null)
        {
            return;
        }

        for (int i = 0; i < stepRuntime.stepRewardList.Count; i++)
        {
            TaskRewardData reward = stepRuntime.stepRewardList[i];
            if (reward == null)
            {
                continue;
            }

            if (reward.rewardType == TaskRewardType.Gold && reward.amount > 0)
            {
                GameMgr.Package.AddGold(reward.amount);
            }
            else if (reward.rewardType == TaskRewardType.Item && reward.itemID >= 0 && reward.amount > 0)
            {
                await GameMgr.Package.AddItem(reward.itemID, reward.amount);
            }
        }
    }

    private void RefreshTrackedTaskUI()
    {
        TaskRuntime trackedRuntime = GetTrackedTask();
        PlayerMainPanel playerMainPanel = GameMgr.UI.GetPanelWithoutLoad<PlayerMainPanel>();
        if (playerMainPanel == null)
        {
            return;
        }

        if (trackedRuntime == null)
        {
            playerMainPanel.SetTrackedTaskRuntime(null);
            return;
        }

        playerMainPanel.SetTrackedTaskRuntime(trackedRuntime);
    }

    private void SaveTasks()
    {
        if (GameMgr.File == null || GameMgr.File.CurrentGameFile == null)
        {
            return;
        }

        GameMgr.File.SaveGameFile();
    }

    private void QueueRuntimeSave()
    {
        if (runtimeSaveQueued)
            return;

        runtimeSaveQueued = true;
        SaveTasksDeferredAsync().Forget();
    }

    private async UniTaskVoid SaveTasksDeferredAsync()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(RuntimeUpdateSaveDebounceSeconds), DelayType.UnscaledDeltaTime);
        runtimeSaveQueued = false;
        SaveTasks();
    }

    private TaskData ResolveTaskData(int taskID)
    {
        foreach (TaskRuntime runtime in acceptedTasks.Values)
        {
            if (runtime?.sourceTaskDataSO == null)
            {
                continue;
            }

            TaskData runtimeTaskData = runtime.sourceTaskDataSO.GetTaskData(taskID);
            if (runtimeTaskData != null)
            {
                return runtimeTaskData;
            }
        }

        TaskDataSO[] loadedTaskDataSOs = Resources.FindObjectsOfTypeAll<TaskDataSO>();
        for (int i = 0; i < loadedTaskDataSOs.Length; i++)
        {
            TaskDataSO taskDataSO = loadedTaskDataSOs[i];
            if (taskDataSO == null)
            {
                continue;
            }

            TaskData taskData = taskDataSO.GetTaskData(taskID);
            if (taskData != null)
            {
                return taskData;
            }
        }

        return null;
    }

    private static List<TaskRewardData> CloneRewardList(List<TaskRewardData> source)
    {
        List<TaskRewardData> result = new List<TaskRewardData>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TaskRewardData reward = source[i];
            if (reward == null)
            {
                continue;
            }

            result.Add(new TaskRewardData
            {
                rewardType = reward.rewardType,
                amount = reward.amount,
                itemID = reward.itemID,
                equipmentID = reward.equipmentID,
                unlockTaskID = reward.unlockTaskID,
                customRewardID = reward.customRewardID
            });
        }

        return result;
    }
}
