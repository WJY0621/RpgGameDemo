using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskGraphNodeType
{
    TaskRoot,
    Step,
    Reward,
    End
}

[Serializable]
public class TaskGraphNodeSaveData
{
    public TaskGraphNodeType nodeType;
    public string id;
    public Vector2 position;
    public string nextNodeID;

    public int taskID = 1;
    public string taskName;
    [TextArea(2, 4)] public string taskDescription;
    public TaskType taskType = TaskType.Side;
    public int giverNpcID = -1;
    public int prerequisiteTaskID = -1;

    public int stepID = 1;
    public string stepName;
    public string stepDescription;
    public List<TaskObjectiveData> objectiveList = new List<TaskObjectiveData>();
    public int goldAmount;
    public List<TaskRewardData> rewardList = new List<TaskRewardData>();
}

public class TaskGraphSaveDataSO : ScriptableObject
{
    public string graphName;
    public TaskDataSO targetTaskDataSO;
    public List<TaskGraphNodeSaveData> nodes = new List<TaskGraphNodeSaveData>();
}
