using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponItem : Item
{
    public int attackPower;
    public float attackSpeed;
}

[Serializable]
public class ConsumableItem : Item
{
    public int Hp;
    public int MP;
}

[Serializable]
public class MaterialItem : Item
{
    public int materialType;
    public int materialAmount;
}
