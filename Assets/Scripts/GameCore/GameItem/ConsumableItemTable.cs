using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameItem/ConsumableItemTable", fileName = "ConsumableTable")]
public class ConsumableItemTable : ScriptableObject
{
    public List<ConsumableItem> Items = new List<ConsumableItem>();
}
