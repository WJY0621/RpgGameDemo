using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DLSO : ScriptableObject
{
    [field: SerializeField]public string DialogueName{ get; set; }
    [field: SerializeField] [field: TextArea()]public string Text{ get; set; }
    [field: SerializeField]public List<DLChoiceData> Choices{ get; set; }
    [field: SerializeField]public DialogueType DialogueType{ get; set; }
    [field: SerializeField]public bool IsStartingDialogue { get; set; }
    [field: SerializeField] public DialogueEventType EventType { get; set; }
    [field: SerializeField] public TaskDataSO TaskDataSO { get; set; }
    [field: SerializeField] public int TaskID { get; set; }
    [field: SerializeField] public int TaskStepID { get; set; }
    [field: SerializeField] public string CustomEventName { get; set; }

    public void Initialize(string dialogueName, string text, List<DLChoiceData> choices, DialogueType dialogueType, bool isStartingDialogue)
    {
        DialogueName = dialogueName;
        Text = text;
        Choices = choices;
        DialogueType = dialogueType;
        IsStartingDialogue = isStartingDialogue;
        EventType = DialogueEventType.None;
        TaskDataSO = null;
        TaskID = -1;
        TaskStepID = -1;
        CustomEventName = string.Empty;
    }

    public void SetEventData(DialogueEventType eventType, TaskDataSO taskDataSO, int taskID, int taskStepID, string customEventName)
    {
        EventType = eventType;
        TaskDataSO = taskDataSO;
        TaskID = taskID;
        TaskStepID = taskStepID;
        CustomEventName = customEventName;
    }
}
