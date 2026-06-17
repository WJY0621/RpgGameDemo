using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskType
{
    Main,
    Side,
    Daily,
    Hidden,
    Tutorial
}

[Serializable]
public class TaskData
{
    [Header("Identity")]
    public int taskID;
    public string taskName;
    [TextArea(2, 5)] public string taskDescription;
    public TaskType taskType = TaskType.Side;

    [Header("NPC")]
    public int giverNpcID = -1;

    [Header("Condition")]
    public List<int> prerequisiteTaskIDs = new List<int>();

    [Header("Content")]
    public List<TaskStepData> stepList = new List<TaskStepData>();
    public List<TaskRewardData> rewardList = new List<TaskRewardData>();

    public TaskRuntime CreateRuntime()
    {
        TaskRuntime runtime = new TaskRuntime();
        runtime.Initialize(this);
        return runtime;
    }
}
