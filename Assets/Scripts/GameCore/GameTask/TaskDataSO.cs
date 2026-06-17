using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TaskDataSO", menuName = "Data/GameTask/TaskDataSO")]
public class TaskDataSO : ScriptableObject
{
    public List<TaskData> taskList = new List<TaskData>();

    public TaskData GetTaskData(int taskID)
    {
        return taskList.Find(task => task != null && task.taskID == taskID);
    }

    public bool ContainsTask(int taskID)
    {
        return GetTaskData(taskID) != null;
    }

    public TaskRuntime CreateRuntime(int taskID)
    {
        TaskData taskData = GetTaskData(taskID);
        TaskRuntime runtime = taskData?.CreateRuntime();
        if (runtime != null)
        {
            runtime.sourceTaskDataSO = this;
        }

        return runtime;
    }

    public void UpsertTaskData(TaskData taskData)
    {
        if (taskData == null)
        {
            return;
        }

        int index = taskList.FindIndex(task => task != null && task.taskID == taskData.taskID);
        if (index >= 0)
        {
            taskList[index] = taskData;
            return;
        }

        taskList.Add(taskData);
    }
}
