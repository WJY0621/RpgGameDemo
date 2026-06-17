using System;

[Serializable]
public class TaskObjectiveSaveData
{
    public int objectiveID;
    public TaskStepType objectiveType;

    public int currentCount;
    public int targetCount;
    public bool isCompleted;

    public int targetNpcID;
    public int targetMonsterID;
    public int targetItemID;
    public string targetAreaID;
    public string targetDialogueGroup;
    public string customTargetID;
}
