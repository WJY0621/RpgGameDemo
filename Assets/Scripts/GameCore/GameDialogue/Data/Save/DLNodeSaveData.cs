using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class DLNodeSaveData
{
    [field: SerializeField] public string ID{ get; set; }
    [field: SerializeField] public string Name{ get; set; } 
    [field: SerializeField] public string Text{ get; set; }
    [field: SerializeField] public List<DLChoiceSaveData> Choices{ get; set; }
    [field: SerializeField] public string GroupID{ get; set; }
    [field: SerializeField] public DialogueType DialogueType{ get; set; }
    [field: SerializeField] public Vector2 Position{ get; set; }
}
