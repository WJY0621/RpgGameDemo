using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameItem/WeaponItemTable", fileName = "WeaponTable")]
public class WeaponItemTable : ScriptableObject
{
    public List<WeaponItem> Items = new List<WeaponItem>();
}
