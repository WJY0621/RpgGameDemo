using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "PlayerInitialData",menuName = "Data/GameCharacter/PlayerInitialData")]
public class PlayerInitialDataSO : ScriptableObject
{
    public int HP;
    public int MP;
    public int ATK;
    public int DEF;
    public int GC;

    public PlayerData GetPlayerInitialData()
    {
        return new PlayerData()
        {
            maxHP = HP,
            currentHP = HP,
            maxMP = MP,
            currentMP = MP,
            currentATK = ATK,
            currentDEF = DEF,
            GC = GC
        };
    }
}
