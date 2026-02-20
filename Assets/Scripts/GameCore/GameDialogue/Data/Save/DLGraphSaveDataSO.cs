using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DLGraphSaveDataSO : ScriptableObject
{
    [field: SerializeField]public string FileName{ get; set; }
    [field: SerializeField]public List<DLGroupSaveData> Groups{ get; set; }
    [field: SerializeField]public List<DLNodeSaveData> Nodes{ get; set; }
    [field: SerializeField]public List<string> OldGroupNames{ get; set; }
    [field: SerializeField]public List<string> OldUnGroupedNodeNames{ get; set; }
    [field: SerializeField]public SerializableDictionary<string, List<string>> OldGroupedNodeNames{get; set; }

    public void Initialize(string fileName)
    {
        FileName = fileName;

        Groups = new List<DLGroupSaveData>();
        Nodes = new List<DLNodeSaveData>();
    }
}
