using System;
using UnityEngine;
[Serializable]
public class DLGroupSaveData
{
    [field: SerializeField]public string ID{ get; set; }
    [field: SerializeField]public string Name{ get; set; }
    [field: SerializeField]public Vector2 Position{ get; set; }
}
