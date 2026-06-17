using System;
using UnityEngine;

[Serializable]
public class TaskObjectiveData
{
    [Header("Identity")]
    public int objectiveID;
    public TaskStepType objectiveType = TaskStepType.Dialogue;

    [Header("Progress")]
    public int targetCount = 1;

    [Header("Target")]
    public int targetNpcID = -1;
    public int targetMonsterID = -1;
    public int targetItemID = -1;
    public string targetAreaID;
    public string targetDialogueGroup = "Default";
    public string customTargetID;

    public TaskObjectiveRuntime CreateRuntime()
    {
        TaskObjectiveRuntime runtime = new TaskObjectiveRuntime();
        runtime.Initialize(this);
        return runtime;
    }
}
