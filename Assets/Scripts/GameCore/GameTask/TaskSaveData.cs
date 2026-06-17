using System;
using System.Collections.Generic;

[Serializable]
public class TaskSaveData
{
    public int taskID;
    public string taskName;
    public string taskDescription;
    public TaskType taskType;

    public int giverNpcID;

    public TaskRuntimeState state;
    public int currentStepIndex;
    public List<TaskStepSaveData> stepSaveList = new List<TaskStepSaveData>();
}
