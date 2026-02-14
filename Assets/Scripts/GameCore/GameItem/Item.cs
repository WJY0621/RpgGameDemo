using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class Item
{
    public int id;
    public string name;
    public ItemType itemType;
    public ItemQuality quality;
    public string description;
    public string functionDescription;
    public int capacity;
    public int buyPrice;
    public int sellPrice;
    public string iconName; // 图片名字
}
public enum ItemType
{
    Weapon = 1,      // 装备
    Consumable = 2,  // 消耗品
    Material = 3     // 材料
}

public enum ItemQuality
{
    Common,
    Advanced,
    Rare,
    Epic,
    Legendary
}