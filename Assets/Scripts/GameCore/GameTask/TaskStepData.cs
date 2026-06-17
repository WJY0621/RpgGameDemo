using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskStepType
{
    Dialogue,
    Kill,
    Collect
}

[Serializable]
public class TaskStepData
{
    [Header("Identity")]
    public int stepID;
    public string stepName;
    [TextArea(2, 4)] public string stepDescription;

    [Header("Objectives")]
    public List<TaskObjectiveData> objectiveList = new List<TaskObjectiveData>();

    [Header("Step Rewards")]
    public List<TaskRewardData> stepRewardList = new List<TaskRewardData>();

    [Header("Legacy Single Objective")]
    public TaskStepType stepType = TaskStepType.Dialogue;
    public int targetCount = 1;
    public int targetNpcID = -1;
    public int targetMonsterID = -1;
    public int targetItemID = -1;
    public string targetAreaID;
    public string targetDialogueGroup = "Default";
    public string customTargetID;

    public bool HasObjectives()
    {
        return objectiveList != null && objectiveList.Count > 0;
    }

    public TaskStepRuntime CreateRuntime()
    {
        TaskStepRuntime runtime = new TaskStepRuntime();
        runtime.Initialize(this);
        return runtime;
    }
}
