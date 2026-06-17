using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class DLGroupErrorData 
{
    public DLErrorData ErrorData { get; set; }
    public List<DLGroup> Groups{ get; set; }

    public DLGroupErrorData()
    {
        ErrorData = new DLErrorData();
        Groups = new List<DLGroup>();
    }
}
