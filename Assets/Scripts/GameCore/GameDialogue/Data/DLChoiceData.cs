using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class DLChoiceData 
{
    [field: SerializeField]public string Text{ get; set; }
    [field: SerializeField]public DLSO NextDialogue{ get; set; }
}
