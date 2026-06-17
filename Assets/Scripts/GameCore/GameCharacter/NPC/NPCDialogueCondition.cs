using System;

[Serializable]
public enum NPCDialogueConditionType
{
    TaskAvailable,
    TaskNotAccepted,
    TaskStepInProgress,
    TaskCompleted
}

[Serializable]
public class NPCDialogueCondition
{
    [UnityEngine.Tooltip("Task ID used to decide whether this dialogue rule is active.")]
    public int taskID = -1;
    [UnityEngine.Tooltip("Step ID used when condition type is TaskStepInProgress.")]
    public int stepID = -1;
    public NPCDialogueConditionType conditionType = NPCDialogueConditionType.TaskNotAccepted;
    public string dialogueGroupName = "Default";
}
