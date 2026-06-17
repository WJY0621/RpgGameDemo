using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopItemData
{
    public int itemId;
    public int stock = -1;
}

[CreateAssetMenu(fileName = "ShopData", menuName = "Game/Shop Data")]
public class ShopDataSO : ScriptableObject
{
    public string shopID;
    public string shopName;
    public List<ShopItemData> sellItems = new List<ShopItemData>();

    public ShopItemData GetSellItem(int itemId)
    {
        if (sellItems == null)
        {
            return null;
        }

        return sellItems.Find(item => item != null && item.itemId == itemId);
    }
}
