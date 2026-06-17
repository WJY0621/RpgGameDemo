using System;
using System.Collections.Generic;

[Serializable]
public class TaskSystemSaveData
{
    public List<TaskSaveData> acceptedTaskSaves = new List<TaskSaveData>();
    public List<int> completedTaskIDs = new List<int>();
    public int trackedTaskID = -1;
}
