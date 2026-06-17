using System;
using UnityEngine;

public enum TaskRewardType
{
    Gold,
    Item,
    Equipment,
    UnlockTask,
    Custom
}

[Serializable]
public class TaskRewardData
{
    [Header("Type")]
    public TaskRewardType rewardType = TaskRewardType.Gold;

    [Header("Value")]
    public int amount = 1;
    public int itemID = -1;
    public int equipmentID = -1;
    public int unlockTaskID = -1;
    public string customRewardID;
}
