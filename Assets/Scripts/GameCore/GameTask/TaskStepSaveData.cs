using System;
using System.Collections.Generic;

[Serializable]
public class TaskStepSaveData
{
    public int stepID;
    public string stepName;
    public string stepDescription;
    public TaskStepType stepType;

    public int currentCount;
    public int targetCount;
    public bool isCompleted;
    public bool stepRewardsGranted;

    public int targetNpcID;
    public int targetMonsterID;
    public int targetItemID;
    public string targetAreaID;
    public string targetDialogueGroup;
    public string customTargetID;

    public List<TaskObjectiveSaveData> objectiveSaveList = new List<TaskObjectiveSaveData>();
    public List<TaskRewardData> stepRewardList = new List<TaskRewardData>();
}
