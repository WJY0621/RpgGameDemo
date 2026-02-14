using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameItem/MaterialItemTable", fileName = "MaterialTable")]
public class MaterialItemTable : ScriptableObject
{
    public List<MaterialItem> Items = new List<MaterialItem>();
}
