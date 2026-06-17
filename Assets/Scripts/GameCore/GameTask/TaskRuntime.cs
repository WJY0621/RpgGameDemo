using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskRuntimeState
{
    Unaccepted,
    InProgress,
    CanSubmit,
    Completed,
    Failed
}

[Serializable]
public class TaskRuntime
{
    [NonSerialized] public TaskDataSO sourceTaskDataSO;
    public int taskID;
    public string taskName;
    public string taskDescription;
    public TaskType taskType;

    public int giverNpcID;

    public TaskRuntimeState state;
    public int currentStepIndex;
    public List<TaskStepRuntime> stepRuntimes = new List<TaskStepRuntime>();

    public void Initialize(TaskData taskData)
    {
        if (taskData == null)
        {
            return;
        }

        taskID = taskData.taskID;
        taskName = taskData.taskName;
        taskDescription = taskData.taskDescription;
        taskType = taskData.taskType;
        giverNpcID = taskData.giverNpcID;

        currentStepIndex = 0;
        state = TaskRuntimeState.InProgress;
        stepRuntimes = new List<TaskStepRuntime>();

        if (taskData.stepList == null)
        {
            return;
        }

        foreach (TaskStepData stepData in taskData.stepList)
        {
            if (stepData == null)
            {
                continue;
            }

            TaskStepRuntime stepRuntime = new TaskStepRuntime();
            stepRuntime.Initialize(stepData);
            stepRuntimes.Add(stepRuntime);
        }

        RefreshState();
    }

    public TaskStepRuntime GetCurrentStep()
    {
        if (stepRuntimes == null || stepRuntimes.Count == 0)
        {
            return null;
        }

        if (currentStepIndex < 0 || currentStepIndex >= stepRuntimes.Count)
        {
            return null;
        }

        return stepRuntimes[currentStepIndex];
    }

    public bool AdvanceCurrentStep(int amount = 1)
    {
        TaskStepRuntime currentStep = GetCurrentStep();
        if (currentStep == null)
        {
            RefreshState();
            return false;
        }

        bool changed = currentStep.AddProgress(amount);
        if (currentStep.isCompleted)
        {
            MoveToNextPendingStep();
        }

        RefreshState();
        return changed;
    }

    public void CompleteCurrentStep()
    {
        TaskStepRuntime currentStep = GetCurrentStep();
        if (currentStep == null)
        {
            RefreshState();
            return;
        }

        currentStep.Complete();
        MoveToNextPendingStep();
        RefreshState();
    }

    public bool TryHandleNpcDialogue(string npcID)
    {
        TaskStepRuntime currentStep = GetCurrentStep();
        if (currentStep == null)
        {
            return false;
        }

        bool changed = currentStep.TryHandleNpcDialogue(npcID);
        if (!changed)
        {
            return false;
        }

        if (currentStep.isCompleted)
        {
            MoveToNextPendingStep();
        }

        RefreshState();
        return true;
    }

    public bool TryHandleMonsterKilled(int monsterID)
    {
        TaskStepRuntime currentStep = GetCurrentStep();
        if (currentStep == null)
        {
            return false;
        }

        bool changed = currentStep.TryHandleMonsterKilled(monsterID);
        if (!changed)
        {
            return false;
        }

        if (currentStep.isCompleted)
        {
            MoveToNextPendingStep();
        }

        RefreshState();
        return true;
    }

    public bool IsCompleted()
    {
        return state == TaskRuntimeState.Completed;
    }

    public void MarkCompleted()
    {
        state = TaskRuntimeState.Completed;
    }

    public bool CanSubmit()
    {
        return state == TaskRuntimeState.CanSubmit;
    }

    private void MoveToNextPendingStep()
    {
        if (stepRuntimes == null || stepRuntimes.Count == 0)
        {
            return;
        }

        for (int i = 0; i < stepRuntimes.Count; i++)
        {
            if (!stepRuntimes[i].isCompleted)
            {
                currentStepIndex = i;
                return;
            }
        }

        currentStepIndex = stepRuntimes.Count - 1;
    }

    private void RefreshState()
    {
        if (stepRuntimes == null || stepRuntimes.Count == 0)
        {
            state = TaskRuntimeState.Completed;
            return;
        }

        bool allStepsCompleted = true;
        foreach (TaskStepRuntime stepRuntime in stepRuntimes)
        {
            if (stepRuntime == null || !stepRuntime.isCompleted)
            {
                allStepsCompleted = false;
                break;
            }
        }

        if (!allStepsCompleted)
        {
            state = TaskRuntimeState.InProgress;
            return;
        }

        state = TaskRuntimeState.Completed;
    }
}
