using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DLContainerSO : ScriptableObject
{
    [field: SerializeField] public string FileName { get; set; }
    [field: SerializeField] public SerializableDictionary<DLGroupSO, List<DLSO>> DialogueGroups { get; set; }
    [field: SerializeField] public List<DLSO> UngroupedDialogues { get; set; }

    public void Initialize(string fileName)
    {
        FileName = fileName;
        DialogueGroups = new SerializableDictionary<DLGroupSO, List<DLSO>>();
        UngroupedDialogues = new List<DLSO>();
    }
    public List<string> GetDialogueGroupNames()
    {
        List<string> dialogueGroupNames = new List<string>();

        foreach (DLGroupSO dialogueGroup in DialogueGroups.Keys)
        {
            dialogueGroupNames.Add(dialogueGroup.GroupName);
        }

        return dialogueGroupNames;
    }
}
