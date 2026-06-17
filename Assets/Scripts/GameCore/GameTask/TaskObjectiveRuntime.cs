using System;

[Serializable]
public class TaskObjectiveRuntime
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

    public void Initialize(TaskObjectiveData objectiveData)
    {
        if (objectiveData == null)
        {
            return;
        }

        objectiveID = objectiveData.objectiveID;
        objectiveType = objectiveData.objectiveType;
        currentCount = 0;
        targetCount = Math.Max(1, objectiveData.targetCount);
        isCompleted = false;

        targetNpcID = objectiveData.targetNpcID;
        targetMonsterID = objectiveData.targetMonsterID;
        targetItemID = objectiveData.targetItemID;
        targetAreaID = objectiveData.targetAreaID;
        targetDialogueGroup = objectiveData.targetDialogueGroup;
        customTargetID = objectiveData.customTargetID;
    }

    public bool AddProgress(int amount = 1)
    {
        if (isCompleted)
        {
            return false;
        }

        currentCount = Math.Min(targetCount, currentCount + Math.Max(1, amount));
        isCompleted = currentCount >= targetCount;
        return true;
    }

    public bool Complete()
    {
        if (isCompleted)
        {
            return false;
        }

        currentCount = targetCount;
        isCompleted = true;
        return true;
    }

    public bool TryHandleNpcDialogue(string npcID)
    {
        if (isCompleted || string.IsNullOrWhiteSpace(npcID))
        {
            return false;
        }

        if (objectiveType != TaskStepType.Dialogue)
        {
            return false;
        }

        if (targetNpcID < 0 || targetNpcID.ToString() != npcID)
        {
            return false;
        }

        return AddProgress(1);
    }

    public bool TryHandleMonsterKilled(int monsterID)
    {
        if (isCompleted || objectiveType != TaskStepType.Kill)
        {
            return false;
        }

        if (targetMonsterID != monsterID)
        {
            return false;
        }

        return AddProgress(1);
    }

    public string BuildDisplayText()
    {
        switch (objectiveType)
        {
            case TaskStepType.Dialogue:
                return $"请和{TaskDisplayNameResolver.ResolveNpcName(targetNpcID)}对话";
            case TaskStepType.Kill:
                return $"请消灭{TaskDisplayNameResolver.ResolveMonsterName(targetMonsterID)}({currentCount}/{targetCount})";
            case TaskStepType.Collect:
                return $"请收集{TaskDisplayNameResolver.ResolveItemName(targetItemID)}({currentCount}/{targetCount})";
            default:
                return objectiveType.ToString();
        }
    }
}
