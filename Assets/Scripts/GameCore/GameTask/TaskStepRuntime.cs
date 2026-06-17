using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class TaskStepRuntime
{
    public int stepID;
    public string stepName;
    public string stepDescription;

    public TaskStepType stepType;
    public int currentCount;
    public int targetCount;
    public bool isCompleted;

    public int targetNpcID;
    public int targetMonsterID;
    public int targetItemID;
    public string targetAreaID;
    public string targetDialogueGroup;
    public string customTargetID;

    public List<TaskObjectiveRuntime> objectiveRuntimes = new List<TaskObjectiveRuntime>();
    public List<TaskRewardData> stepRewardList = new List<TaskRewardData>();
    public bool stepRewardsGranted;

    public void Initialize(TaskStepData stepData)
    {
        if (stepData == null)
        {
            return;
        }

        stepID = stepData.stepID;
        stepName = stepData.stepName;
        stepDescription = stepData.stepDescription;
        objectiveRuntimes = new List<TaskObjectiveRuntime>();
        stepRewardList = CloneRewardList(stepData.stepRewardList);
        stepRewardsGranted = false;

        if (stepData.HasObjectives())
        {
            foreach (TaskObjectiveData objectiveData in stepData.objectiveList)
            {
                if (objectiveData == null)
                {
                    continue;
                }

                TaskObjectiveRuntime runtime = new TaskObjectiveRuntime();
                runtime.Initialize(objectiveData);
                objectiveRuntimes.Add(runtime);
            }

            RefreshSummaryFromObjectives();
            return;
        }

        BuildLegacyRuntime(stepData);
    }

    public bool AddProgress(int amount = 1)
    {
        if (objectiveRuntimes != null && objectiveRuntimes.Count > 0)
        {
            TaskObjectiveRuntime targetObjective = objectiveRuntimes.FirstOrDefault(objective => objective != null && !objective.isCompleted);
            if (targetObjective == null)
            {
                return false;
            }

            bool changed = targetObjective.AddProgress(amount);
            RefreshSummaryFromObjectives();
            return changed;
        }

        if (isCompleted)
        {
            return false;
        }

        currentCount = Math.Min(targetCount, currentCount + Math.Max(1, amount));
        isCompleted = currentCount >= targetCount;
        return true;
    }

    public bool Complete()
    {
        if (objectiveRuntimes != null && objectiveRuntimes.Count > 0)
        {
            bool changed = false;
            foreach (TaskObjectiveRuntime objective in objectiveRuntimes)
            {
                if (objective != null)
                {
                    changed |= objective.Complete();
                }
            }

            RefreshSummaryFromObjectives();
            return changed;
        }

        if (isCompleted)
        {
            return false;
        }

        currentCount = targetCount;
        isCompleted = true;
        return true;
    }

    public bool TryHandleNpcDialogue(string npcID)
    {
        if (objectiveRuntimes != null && objectiveRuntimes.Count > 0)
        {
            foreach (TaskObjectiveRuntime objective in objectiveRuntimes)
            {
                if (objective != null && objective.TryHandleNpcDialogue(npcID))
                {
                    RefreshSummaryFromObjectives();
                    return true;
                }
            }

            return false;
        }

        if (stepType != TaskStepType.Dialogue)
        {
            return false;
        }

        if (targetNpcID < 0 || targetNpcID.ToString() != npcID)
        {
            return false;
        }

        return AddProgress(1);
    }

    public bool TryHandleMonsterKilled(int monsterID)
    {
        if (objectiveRuntimes != null && objectiveRuntimes.Count > 0)
        {
            foreach (TaskObjectiveRuntime objective in objectiveRuntimes)
            {
                if (objective != null && objective.TryHandleMonsterKilled(monsterID))
                {
                    RefreshSummaryFromObjectives();
                    return true;
                }
            }

            return false;
        }

        if (stepType != TaskStepType.Kill || targetMonsterID != monsterID)
        {
            return false;
        }

        return AddProgress(1);
    }

    public float GetNormalizedProgress()
    {
        if (targetCount <= 0)
        {
            return 0f;
        }

        return (float)currentCount / targetCount;
    }

    public string BuildDisplayText()
    {
        string baseText = string.IsNullOrWhiteSpace(stepDescription) ? stepName : stepDescription;

        if (objectiveRuntimes == null || objectiveRuntimes.Count == 0)
        {
            return BuildLegacyDisplayText(baseText);
        }

        if (objectiveRuntimes.Count == 1)
        {
            return objectiveRuntimes[0].BuildDisplayText();
        }

        List<string> lines = new List<string>();
        string headerText = string.IsNullOrWhiteSpace(stepName) ? baseText : stepName;
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            lines.Add(headerText);
        }

        foreach (TaskObjectiveRuntime objective in objectiveRuntimes)
        {
            if (objective != null)
            {
                lines.Add($"- {objective.BuildDisplayText()}");
            }
        }

        return string.Join("\n", lines);
    }

    private string BuildLegacyDisplayText(string fallbackText)
    {
        switch (stepType)
        {
            case TaskStepType.Dialogue:
                return targetNpcID >= 0
                    ? $"请和{TaskDisplayNameResolver.ResolveNpcName(targetNpcID)}对话"
                    : fallbackText;
            case TaskStepType.Kill:
                return targetMonsterID >= 0
                    ? $"请消灭{TaskDisplayNameResolver.ResolveMonsterName(targetMonsterID)}({currentCount}/{targetCount})"
                    : $"{fallbackText} ({currentCount}/{targetCount})";
            case TaskStepType.Collect:
                return targetItemID >= 0
                    ? $"请收集{TaskDisplayNameResolver.ResolveItemName(targetItemID)}({currentCount}/{targetCount})"
                    : $"{fallbackText} ({currentCount}/{targetCount})";
            default:
                return fallbackText;
        }
    }

    public void RefreshSummaryFromObjectives()
    {
        if (objectiveRuntimes == null || objectiveRuntimes.Count == 0)
        {
            return;
        }

        if (objectiveRuntimes.Count == 1)
        {
            TaskObjectiveRuntime objective = objectiveRuntimes[0];
            if (objective == null)
            {
                return;
            }

            stepType = objective.objectiveType;
            currentCount = objective.currentCount;
            targetCount = objective.targetCount;
            isCompleted = objective.isCompleted;
            targetNpcID = objective.targetNpcID;
            targetMonsterID = objective.targetMonsterID;
            targetItemID = objective.targetItemID;
            targetAreaID = objective.targetAreaID;
            targetDialogueGroup = objective.targetDialogueGroup;
            customTargetID = objective.customTargetID;
            return;
        }

        TaskObjectiveRuntime firstObjective = objectiveRuntimes.FirstOrDefault(objective => objective != null);
        stepType = firstObjective != null ? firstObjective.objectiveType : TaskStepType.Dialogue;
        currentCount = objectiveRuntimes.Count(objective => objective != null && objective.isCompleted);
        targetCount = objectiveRuntimes.Count;
        isCompleted = objectiveRuntimes.All(objective => objective != null && objective.isCompleted);
        targetNpcID = -1;
        targetMonsterID = -1;
        targetItemID = -1;
        targetAreaID = string.Empty;
        targetDialogueGroup = string.Empty;
        customTargetID = string.Empty;
    }

    private void BuildLegacyRuntime(TaskStepData stepData)
    {
        stepType = stepData.stepType;
        currentCount = 0;
        targetCount = Math.Max(1, stepData.targetCount);
        isCompleted = false;

        targetNpcID = stepData.targetNpcID;
        targetMonsterID = stepData.targetMonsterID;
        targetItemID = stepData.targetItemID;
        targetAreaID = stepData.targetAreaID;
        targetDialogueGroup = stepData.targetDialogueGroup;
        customTargetID = stepData.customTargetID;
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
